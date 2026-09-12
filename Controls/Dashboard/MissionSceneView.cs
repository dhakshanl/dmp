using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// The Mission page's centrepiece: a perspective 3D view of the arena drawn from the
    /// shared MissionScene - explored floor, mapped walls (cut away at waist height so the
    /// rooms stay readable), the drone and its flown trail, survivors, fires with their
    /// keep-out zones, and the highlighted rescue route. Drag to orbit, right-drag to pan,
    /// wheel to zoom, double-click to reset; clicking a survivor selects its route.
    ///
    /// Plain GDI+ with a painter's sort rather than an OpenGL control: the scene is a few
    /// hundred polygons, and a custom-painted Control keeps ThemeManager's hands off it
    /// and adds no native dependency to the page.
    /// </summary>
    public class MissionSceneView : Control
    {
        private const float WallHeight = 1.1f;
        private const float WallThickness = 0.14f;
        private const float DroneScale = 1.7f;
        private const float FovDeg = 38f;
        private const float DefaultYaw = -24f;
        private const float DefaultPitch = 52f;

        private static readonly Color FloorColor = Color.FromArgb(22, 25, 36);
        private static readonly Color GridColor = Color.FromArgb(36, 41, 57);
        private static readonly Color GridMajorColor = Color.FromArgb(50, 57, 78);
        private static readonly Color WallTop = Color.FromArgb(120, 138, 180);
        private static readonly Color WallEdge = Color.FromArgb(110, 160, 180, 220);

        private MissionScene scene;
        private readonly Timer frameTimer;
        private readonly Stopwatch anim = Stopwatch.StartNew();

        private float yawDeg = DefaultYaw;
        private float pitchDeg = DefaultPitch;
        private float zoom = 1f;
        private float targetX = float.NaN;
        private float targetY = float.NaN;

        private Point lastMouse;
        private MouseButtons dragButton;
        private bool dragged;

        // Per-frame camera basis.
        private V3 eye, fwd, right, up;
        private float focal, centreX, centreY;

        private readonly Dictionary<int, PointF> pinHeads = new Dictionary<int, PointF>();
        private readonly List<KeyValuePair<float, Action<Graphics>>> sorted = new List<KeyValuePair<float, Action<Graphics>>>();

        /// <summary>Raised with the id of a survivor pin the operator clicked.</summary>
        public event Action<int> SurvivorClicked;

        /// <summary>Shown centred while the scene has no drone and no map.</summary>
        public string EmptyMessage { get; set; } = "Waiting for vehicle position";

        /// <summary>Second line of the title block, e.g. "Demo scenario" or "Live map".</summary>
        public string Subtitle { get; set; } = "Live map";

        public MissionSceneView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = MissionPalette.Background;

            // Rotors, pulses and route chevrons animate; 20 fps is plenty for that and
            // keeps GDI+ well inside a frame budget.
            frameTimer = new Timer { Interval = 50 };
            frameTimer.Tick += (s, e) => Invalidate();
        }

        public void Bind(MissionScene s)
        {
            scene = s;
            Invalidate();
        }

        /// <summary>Runs the animation clock; the page turns it off while the tab is hidden.</summary>
        public bool Animate
        {
            get { return frameTimer.Enabled; }
            set { frameTimer.Enabled = value; }
        }

        public void ResetView()
        {
            yawDeg = DefaultYaw;
            pitchDeg = DefaultPitch;
            zoom = 1f;
            targetX = targetY = float.NaN;
            Invalidate();
        }

        #region interaction

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            lastMouse = e.Location;
            dragButton = e.Button;
            dragged = false;
            Focus();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragButton == MouseButtons.None)
            {
                Cursor = HitPin(e.Location) != 0 ? Cursors.Hand : Cursors.Default;
                return;
            }

            var dx = e.X - lastMouse.X;
            var dy = e.Y - lastMouse.Y;
            if (Math.Abs(dx) + Math.Abs(dy) > 2)
                dragged = true;
            lastMouse = e.Location;

            if (dragButton == MouseButtons.Left)
            {
                yawDeg -= dx * 0.4f;
                pitchDeg = Math.Max(16f, Math.Min(88f, pitchDeg + dy * 0.3f));
            }
            else if (dragButton == MouseButtons.Right || dragButton == MouseButtons.Middle)
            {
                Pan(dx, dy);
            }

            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragged && e.Button == MouseButtons.Left)
            {
                var id = HitPin(e.Location);
                if (id != 0)
                    SurvivorClicked?.Invoke(id);
            }
            dragButton = MouseButtons.None;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            zoom = Math.Max(0.35f, Math.Min(1.8f, zoom * (e.Delta > 0 ? 0.9f : 1.11f)));
            Invalidate();
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            ResetView();
        }

        private void Pan(int dx, int dy)
        {
            var size = ArenaSize;
            var metresPerPixel = Distance() / Math.Max(1f, focal);
            var fx = fwd.X;
            var fy = fwd.Y;
            var fl = (float)Math.Sqrt(fx * fx + fy * fy);
            if (fl < 1e-3f)
                return;
            fx /= fl;
            fy /= fl;

            var tx = TargetX - right.X * dx * metresPerPixel + fx * dy * metresPerPixel;
            var ty = TargetY - right.Y * dx * metresPerPixel + fy * dy * metresPerPixel;
            targetX = Math.Max(-2f, Math.Min(size + 2f, tx));
            targetY = Math.Max(-2f, Math.Min(size + 2f, ty));
        }

        private int HitPin(Point p)
        {
            foreach (var kv in pinHeads)
            {
                var dx = kv.Value.X - p.X;
                var dy = kv.Value.Y - p.Y;
                if (dx * dx + dy * dy < 16 * 16)
                    return kv.Key;
            }
            return 0;
        }

        #endregion

        #region camera

        private float ArenaSize
        {
            get { return scene != null ? scene.ArenaSize : 15f; }
        }

        private float TargetX
        {
            get { return float.IsNaN(targetX) ? ArenaSize / 2f : targetX; }
        }

        private float TargetY
        {
            get { return float.IsNaN(targetY) ? ArenaSize / 2f - 0.6f : targetY; }
        }

        private float Distance()
        {
            var fit = ArenaSize * 0.64f / (float)Math.Tan(Rad(FovDeg / 2f));
            return fit * zoom;
        }

        private void SetupCamera()
        {
            var yaw = Rad(yawDeg);
            var pitch = Rad(pitchDeg);
            var t = new V3(TargetX, TargetY, 0);
            var dist = Distance();

            eye = t + new V3((float)(Math.Cos(pitch) * Math.Sin(yaw)), (float)(-Math.Cos(pitch) * Math.Cos(yaw)),
                (float)Math.Sin(pitch)) * dist;
            fwd = (t - eye).Normalized();
            right = new V3(fwd.Y, -fwd.X, 0).Normalized();
            up = V3.Cross(right, fwd);

            focal = Math.Min(Height, Width * 0.78f) / 2f / (float)Math.Tan(Rad(FovDeg / 2f));
            centreX = Width / 2f;
            centreY = Height / 2f + Height * 0.03f;
        }

        private bool Project(V3 p, out PointF s)
        {
            var d = p - eye;
            var z = V3.Dot(d, fwd);
            if (z < 0.2f)
            {
                s = PointF.Empty;
                return false;
            }
            s = new PointF(centreX + focal * V3.Dot(d, right) / z, centreY - focal * V3.Dot(d, up) / z);
            return true;
        }

        private bool Project(float x, float y, float z, out PointF s)
        {
            return Project(new V3(x, y, z), out s);
        }

        private float Depth(V3 p)
        {
            return V3.Dot(p - eye, fwd);
        }

        private PointF[] ProjectAll(V3[] pts)
        {
            var result = new PointF[pts.Length];
            for (var i = 0; i < pts.Length; i++)
            {
                if (!Project(pts[i], out result[i]))
                    return null;
            }
            return result;
        }

        #endregion

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            if (Width < 50 || Height < 50)
                return;

            SetupCamera();
            var t = (float)anim.Elapsed.TotalSeconds;
            pinHeads.Clear();

            DrawFloor(g);
            DrawGridLabels(g);
            DrawEntryExit(g);

            if (scene != null)
            {
                foreach (var h in scene.Hazards)
                    DrawHazardZone(g, h, t);
                foreach (var s in scene.Survivors)
                    DrawSurvivorRing(g, s, t);

                var selected = scene.SelectedSurvivor;
                if (selected?.Route != null)
                    DrawRoute(g, selected.Route, t);

                DrawTrail(g);
                if (scene.HasDrone)
                    DrawDroneShadow(g);

                // Walls, pins, flames and the drone are depth-sorted together so a wall in
                // front of a survivor hides it, and one behind does not.
                sorted.Clear();
                foreach (var w in scene.Walls)
                    QueueWall(w);
                foreach (var s in scene.Survivors)
                    QueueSurvivorPin(s);
                foreach (var h in scene.Hazards)
                    QueueHazard(h, t);
                if (scene.HasDrone)
                    QueueDrone(t);

                sorted.Sort((a, b) => b.Key.CompareTo(a.Key));
                foreach (var item in sorted)
                    item.Value(g);
            }

            DrawHud(g);
        }

        #region floor pass

        private void DrawFloor(Graphics g)
        {
            var s = ArenaSize;

            var floor = ProjectAll(new[] { new V3(0, 0, 0), new V3(s, 0, 0), new V3(s, s, 0), new V3(0, s, 0) });
            if (floor != null)
            {
                using (var b = new SolidBrush(FloorColor))
                    g.FillPolygon(b, floor);
            }

            // Explored floor, merged into horizontal runs per row to keep the polygon count low.
            if (scene != null)
            {
                var map = scene.Explored;
                var res = ExploredMap.Resolution;
                using (var b = new SolidBrush(Color.FromArgb(34, MissionPalette.Accent)))
                {
                    for (var cy = 0; cy < map.Size; cy++)
                    {
                        var runStart = -1;
                        for (var cx = 0; cx <= map.Size; cx++)
                        {
                            var on = cx < map.Size && map[cx, cy];
                            if (on && runStart < 0)
                                runStart = cx;
                            else if (!on && runStart >= 0)
                            {
                                var quad = ProjectAll(new[]
                                {
                                    new V3(runStart * res, cy * res, 0), new V3(cx * res, cy * res, 0),
                                    new V3(cx * res, (cy + 1) * res, 0), new V3(runStart * res, (cy + 1) * res, 0)
                                });
                                if (quad != null)
                                    g.FillPolygon(b, quad);
                                runStart = -1;
                            }
                        }
                    }
                }
            }

            using (var minor = new Pen(GridColor, 1f))
            using (var major = new Pen(GridMajorColor, 1f))
            {
                var steps = (int)Math.Round(s / MissionGrid.CellMetres);
                for (var i = 0; i <= steps; i++)
                {
                    var v = i * MissionGrid.CellMetres;
                    var pen = i % 5 == 0 ? major : minor;
                    DrawLine3(g, pen, new V3(v, 0, 0), new V3(v, s, 0));
                    DrawLine3(g, pen, new V3(0, v, 0), new V3(s, v, 0));
                }
            }
        }

        private void DrawGridLabels(Graphics g)
        {
            var steps = (int)Math.Round(ArenaSize / MissionGrid.CellMetres);
            for (var i = 0; i < steps; i++)
            {
                var c = (i + 0.5f) * MissionGrid.CellMetres;
                PointF p;
                if (Project(c, -0.45f, 0, out p))
                    DrawCentredText(g, MissionGrid.ColumnLabel(i), MissionPalette.Small, p, MissionPalette.TextFaint);
                if (Project(-0.45f, c, 0, out p))
                    DrawCentredText(g, (i + 1).ToString(), MissionPalette.Small, p, MissionPalette.TextFaint);
            }
        }

        private void DrawEntryExit(Graphics g)
        {
            if (scene == null)
                return;

            DrawFloorTag(g, new V3(scene.Entry.X, -1.15f, 0), "ENTRY", MissionPalette.TextDim);
            DrawFloorTag(g, new V3(scene.Exit.X, ArenaSize + 1.1f, 0), "EXIT", MissionPalette.TextDim);

            // The 2 ft launch pad.
            const float half = 0.305f;
            var pad = scene.LaunchPad;
            var quad = ProjectAll(new[]
            {
                new V3(pad.X - half, pad.Y - half, 0.01f), new V3(pad.X + half, pad.Y - half, 0.01f),
                new V3(pad.X + half, pad.Y + half, 0.01f), new V3(pad.X - half, pad.Y + half, 0.01f)
            });
            if (quad != null)
            {
                using (var fill = new SolidBrush(Color.FromArgb(40, MissionPalette.Accent)))
                using (var pen = new Pen(Color.FromArgb(170, MissionPalette.Accent), 1.3f))
                {
                    g.FillPolygon(fill, quad);
                    g.DrawPolygon(pen, quad);
                }
            }
        }

        private void DrawHazardZone(Graphics g, Detection h, float t)
        {
            var ring = Circle(h.Position.X, h.Position.Y, 0.015f, h.HazardRadius, 40);
            if (ring == null)
                return;

            var flicker = 0.8f + 0.2f * (float)Math.Sin(t * 6 + h.Id);
            var zoneColor = HazardVisuals.Color(h.Type);
            using (var fill = new SolidBrush(Color.FromArgb((int)(46 * flicker), zoneColor)))
            using (var pen = new Pen(Color.FromArgb(190, zoneColor), 1.4f) { DashStyle = DashStyle.Dash })
            {
                g.FillPolygon(fill, ring);
                g.DrawPolygon(pen, ring);
            }
        }

        private void DrawSurvivorRing(Graphics g, Detection s, float t)
        {
            var phase = (t * 0.7f + s.Id * 0.37f) % 1f;
            var ring = Circle(s.Position.X, s.Position.Y, 0.02f, 0.25f + phase * 0.55f, 28);
            if (ring == null)
                return;

            using (var pen = new Pen(Color.FromArgb((int)(200 * (1 - phase)), MissionPalette.Survivor), 1.6f))
                g.DrawPolygon(pen, ring);

            var core = Circle(s.Position.X, s.Position.Y, 0.02f, 0.22f, 20);
            if (core != null)
            {
                using (var fill = new SolidBrush(Color.FromArgb(90, MissionPalette.Survivor)))
                    g.FillPolygon(fill, core);
            }
        }

        private void DrawRoute(Graphics g, List<PointF> route, float t)
        {
            var pts = new List<PointF>();
            foreach (var p in route)
            {
                PointF s;
                if (!Project(p.X, p.Y, 0.03f, out s))
                    return;
                pts.Add(s);
            }
            if (pts.Count < 2)
                return;

            var arr = pts.ToArray();
            using (var glow = new Pen(Color.FromArgb(60, MissionPalette.Route), 9f) { LineJoin = LineJoin.Round })
                g.DrawLines(glow, arr);
            using (var line = new Pen(MissionPalette.Route, 2.6f) { LineJoin = LineJoin.Round })
                g.DrawLines(line, arr);

            // Chevrons walking toward the survivor, spaced in metres so they sit evenly at any zoom.
            const float spacing = 0.9f;
            var offset = (t * 0.8f) % spacing;
            var walked = 0f;
            using (var chev = new Pen(Color.FromArgb(230, 12, 30, 20), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                for (var i = 1; i < route.Count; i++)
                {
                    var a = route[i - 1];
                    var b = route[i];
                    var len = (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                    if (len < 1e-3f)
                        continue;

                    var ux = (b.X - a.X) / len;
                    var uy = (b.Y - a.Y) / len;
                    for (var d = spacing - ((walked - offset) % spacing + spacing) % spacing; d < len; d += spacing)
                    {
                        var cx = a.X + ux * d;
                        var cy = a.Y + uy * d;
                        PointF tip, l, r;
                        if (Project(cx + ux * 0.12f, cy + uy * 0.12f, 0.035f, out tip) &&
                            Project(cx - ux * 0.08f - uy * 0.12f, cy - uy * 0.08f + ux * 0.12f, 0.035f, out l) &&
                            Project(cx - ux * 0.08f + uy * 0.12f, cy - uy * 0.08f - ux * 0.12f, 0.035f, out r))
                        {
                            g.DrawLines(chev, new[] { l, tip, r });
                        }
                    }
                    walked += len;
                }
            }

            // Arrowhead on the survivor end.
            var last = route[route.Count - 1];
            var prev = route[route.Count - 2];
            var ll = (float)Math.Sqrt((last.X - prev.X) * (last.X - prev.X) + (last.Y - prev.Y) * (last.Y - prev.Y));
            if (ll > 1e-3f)
            {
                var ux = (last.X - prev.X) / ll;
                var uy = (last.Y - prev.Y) / ll;
                var head = ProjectAll(new[]
                {
                    new V3(last.X - ux * 0.05f, last.Y - uy * 0.05f, 0.03f),
                    new V3(last.X - ux * 0.5f - uy * 0.26f, last.Y - uy * 0.5f + ux * 0.26f, 0.03f),
                    new V3(last.X - ux * 0.5f + uy * 0.26f, last.Y - uy * 0.5f - ux * 0.26f, 0.03f)
                });
                if (head != null)
                {
                    using (var b = new SolidBrush(MissionPalette.Route))
                        g.FillPolygon(b, head);
                }
            }
        }

        private void DrawTrail(Graphics g)
        {
            var trail = scene.Trail;
            if (trail.Count < 2)
                return;

            var pts = new List<PointF>(trail.Count);
            foreach (var p in trail)
            {
                PointF s;
                if (Project(p.X, p.Y, p.Z, out s))
                    pts.Add(s);
            }
            if (pts.Count < 2)
                return;

            using (var pen = new Pen(Color.FromArgb(150, MissionPalette.Accent), 1.5f) { LineJoin = LineJoin.Round })
                g.DrawLines(pen, pts.ToArray());
        }

        private void DrawDroneShadow(Graphics g)
        {
            var d = scene.Drone;
            var shadow = Circle(d.X, d.Y, 0.01f, 0.32f * DroneScale * 0.6f, 24);
            if (shadow != null)
            {
                using (var b = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
                    g.FillPolygon(b, shadow);
            }

            if (d.Z > 0.05f)
            {
                PointF top, bottom;
                if (Project(d.X, d.Y, d.Z, out top) && Project(d.X, d.Y, 0.01f, out bottom))
                {
                    using (var pen = new Pen(Color.FromArgb(110, MissionPalette.Accent), 1f) { DashStyle = DashStyle.Dot })
                        g.DrawLine(pen, top, bottom);
                }
            }
        }

        #endregion

        #region sorted objects

        private void QueueWall(Wall w)
        {
            var dx = w.B.X - w.A.X;
            var dy = w.B.Y - w.A.Y;
            var len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-3f)
                return;

            var d = new V3(dx / len, dy / len, 0);
            var n = new V3(-d.Y, d.X, 0);
            var ht = WallThickness / 2f;
            var a = new V3(w.A.X, w.A.Y, 0) - d * ht;
            var b = new V3(w.B.X, w.B.Y, 0) + d * ht;

            V3 a0 = a + n * ht, a1 = a - n * ht, b0 = b + n * ht, b1 = b - n * ht;
            var h = new V3(0, 0, WallHeight);

            QueueFace(new[] { a0, b0, b0 + h, a0 + h }, n);
            QueueFace(new[] { b1, a1, a1 + h, b1 + h }, n * -1f);
            QueueFace(new[] { a1, a0, a0 + h, a1 + h }, d * -1f);
            QueueFace(new[] { b0, b1, b1 + h, b0 + h }, d);
            QueueFace(new[] { a0 + h, b0 + h, b1 + h, a1 + h }, new V3(0, 0, 1));
        }

        private void QueueFace(V3[] quad, V3 normal)
        {
            var centre = (quad[0] + quad[1] + quad[2] + quad[3]) * 0.25f;
            if (V3.Dot(normal, eye - centre) <= 0)
                return;

            var pts = ProjectAll(quad);
            if (pts == null)
                return;

            Color fill;
            if (normal.Z > 0.5f)
            {
                fill = WallTop;
            }
            else
            {
                // Soft key light from the north-west so the four wall orientations read differently.
                var light = Math.Max(0f, normal.X * -0.55f + normal.Y * 0.83f);
                var k = 0.45f + 0.55f * light;
                fill = Color.FromArgb(236, (int)(44 + 46 * k), (int)(50 + 56 * k), (int)(70 + 76 * k));
            }

            sorted.Add(new KeyValuePair<float, Action<Graphics>>(Depth(centre), gr =>
            {
                using (var b = new SolidBrush(fill))
                    gr.FillPolygon(b, pts);
                using (var pen = new Pen(WallEdge, 1f))
                    gr.DrawPolygon(pen, pts);
            }));
        }

        private void QueueSurvivorPin(Detection s)
        {
            var baseP = new V3(s.Position.X, s.Position.Y, 0);
            var headP = new V3(s.Position.X, s.Position.Y, 1.35f);
            PointF b, h;
            if (!Project(baseP, out b) || !Project(headP, out h))
                return;

            pinHeads[s.Id] = h;
            var selected = scene.SelectedSurvivorId == s.Id;

            sorted.Add(new KeyValuePair<float, Action<Graphics>>(Depth(headP) - 0.05f, gr =>
            {
                using (var pen = new Pen(MissionPalette.Survivor, 1.6f))
                    gr.DrawLine(pen, b, h);
                using (var fill = new SolidBrush(MissionPalette.Survivor))
                    gr.FillEllipse(fill, h.X - 4.5f, h.Y - 4.5f, 9, 9);

                var text = s.Name + "  " + s.Grid;
                var size = TextRenderer.MeasureText(gr, text, MissionPalette.SmallBold, Size.Empty, TextFormatFlags.NoPadding);
                var r = new RectangleF(h.X - size.Width / 2f - 7, h.Y - size.Height - 16, size.Width + 14, size.Height + 7);
                using (var path = MissionPalette.RoundedRect(r, 4))
                using (var bg = new SolidBrush(Color.FromArgb(225, 30, 26, 16)))
                using (var border = new Pen(selected ? MissionPalette.Route : MissionPalette.Survivor, selected ? 2f : 1f))
                {
                    gr.FillPath(bg, path);
                    gr.DrawPath(border, path);
                }
                TextRenderer.DrawText(gr, text, MissionPalette.SmallBold, Rectangle.Round(r), MissionPalette.Survivor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }));
        }

        /// <summary>
        /// Renders a hazard pin with an icon matched to its onboard classification (fire,
        /// flood, electrical, structural - the "unified multi-class hazard" output the
        /// SIH26177 architecture produces from fused RGB + thermal), not just a flame.
        /// </summary>
        private void QueueHazard(Detection h, float t)
        {
            var p = new V3(h.Position.X, h.Position.Y, 0);
            PointF baseS;
            if (!Project(p, out baseS))
                return;

            var depth = Depth(p);
            var scale = focal / Math.Max(1f, depth);
            var color = HazardVisuals.Color(h.Type);

            sorted.Add(new KeyValuePair<float, Action<Graphics>>(depth - 0.1f, gr =>
            {
                switch (h.Type)
                {
                    case HazardType.Flood: DrawFloodIcon(gr, baseS, scale, t, h.Id, color); break;
                    case HazardType.Electrical: DrawElectricalIcon(gr, baseS, scale, t, h.Id, color); break;
                    case HazardType.Structural: DrawStructuralIcon(gr, baseS, scale, color); break;
                    default: DrawFireIcon(gr, baseS, scale, t, h.Id, color); break;
                }

                var label = HazardVisuals.Label(h.Type).ToUpperInvariant() + "  " + h.Grid;
                var size = TextRenderer.MeasureText(gr, label, MissionPalette.SmallBold, Size.Empty, TextFormatFlags.NoPadding);
                var top = baseS.Y - 0.95f * scale - size.Height - 10;
                var r = new RectangleF(baseS.X - size.Width / 2f - 7, top, size.Width + 14, size.Height + 7);
                using (var path = MissionPalette.RoundedRect(r, 4))
                using (var bg = new SolidBrush(Color.FromArgb(225, 30, 24, 20)))
                using (var border = new Pen(color, 1f))
                {
                    gr.FillPath(bg, path);
                    gr.DrawPath(border, path);
                }
                TextRenderer.DrawText(gr, label, MissionPalette.SmallBold, Rectangle.Round(r), color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }));
        }

        private static void DrawFireIcon(Graphics g, PointF baseS, float scale, float t, int id, Color color)
        {
            // Three tongues of flame, each flickering on its own phase.
            float[] offsets = { -0.16f, 0.14f, 0f };
            float[] heights = { 0.55f, 0.6f, 0.85f };
            for (var i = 0; i < 3; i++)
            {
                var flick = 1f + 0.18f * (float)Math.Sin(t * (8 + i * 1.7) + i * 2.1 + id);
                var ht = heights[i] * scale * flick;
                var w = ht * 0.42f;
                var x = baseS.X + offsets[i] * scale;
                DrawFlame(g, x, baseS.Y, w, ht, color);
                DrawFlame(g, x, baseS.Y, w * 0.55f, ht * 0.6f, Color.FromArgb(255, 214, 110));
            }
        }

        /// <summary>A shimmering puddle with ripple rings expanding outward.</summary>
        private static void DrawFloodIcon(Graphics g, PointF baseS, float scale, float t, int id, Color color)
        {
            var puddle = new RectangleF(baseS.X - 0.5f * scale, baseS.Y - 0.12f * scale, scale, 0.24f * scale);
            using (var fill = new SolidBrush(Color.FromArgb(120, color)))
                g.FillEllipse(fill, puddle);

            for (var i = 0; i < 3; i++)
            {
                var phase = ((t * 0.5f + id * 0.3f) + i / 3f) % 1f;
                var rw = (0.25f + phase * 0.55f) * scale;
                var rh = rw * 0.32f;
                using (var pen = new Pen(Color.FromArgb((int)(200 * (1 - phase)), color), 1.6f))
                    g.DrawEllipse(pen, baseS.X - rw / 2, baseS.Y - rh / 2, rw, rh);
            }
        }

        /// <summary>A flickering lightning-bolt glyph for an exposed-electrical hazard.</summary>
        private static void DrawElectricalIcon(Graphics g, PointF baseS, float scale, float t, int id, Color color)
        {
            var flick = 0.75f + 0.25f * (float)Math.Sin(t * 14 + id * 1.7);
            var h = 0.85f * scale;
            var w = 0.34f * scale;
            var top = baseS.Y - h;
            var pts = new[]
            {
                new PointF(baseS.X + w * 0.15f, top),
                new PointF(baseS.X - w * 0.35f, top + h * 0.55f),
                new PointF(baseS.X, top + h * 0.55f),
                new PointF(baseS.X - w * 0.15f, top + h),
                new PointF(baseS.X + w * 0.35f, top + h * 0.4f),
                new PointF(baseS.X + w * 0.02f, top + h * 0.4f)
            };

            using (var glow = new SolidBrush(Color.FromArgb((int)(70 * flick), color)))
                g.FillEllipse(glow, baseS.X - w, baseS.Y - h, w * 2, h);
            using (var fill = new SolidBrush(Color.FromArgb((int)(255 * flick), color)))
                g.FillPolygon(fill, pts);
        }

        /// <summary>A warning triangle for a structural-collapse risk.</summary>
        private static void DrawStructuralIcon(Graphics g, PointF baseS, float scale, Color color)
        {
            var h = 0.8f * scale;
            var apex = new PointF(baseS.X, baseS.Y - h);
            var left = new PointF(baseS.X - 0.42f * scale, baseS.Y);
            var right = new PointF(baseS.X + 0.42f * scale, baseS.Y);
            var tri = new[] { apex, right, left };

            using (var fill = new SolidBrush(Color.FromArgb(210, 40, 30, 20)))
            using (var border = new Pen(color, 1.6f) { LineJoin = LineJoin.Round })
            {
                g.FillPolygon(fill, tri);
                g.DrawPolygon(border, tri);
            }

            var markRect = new Rectangle((int)(baseS.X - 8), (int)(baseS.Y - h * 0.75f), 16, (int)(h * 0.55f));
            TextRenderer.DrawText(g, "!", MissionPalette.BodyBold, markRect, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static void DrawFlame(Graphics g, float x, float baseY, float w, float h, Color color)
        {
            using (var path = new GraphicsPath())
            {
                path.AddBezier(x - w / 2, baseY, x - w * 0.7f, baseY - h * 0.45f, x - w * 0.1f, baseY - h * 0.7f, x, baseY - h);
                path.AddBezier(x, baseY - h, x + w * 0.15f, baseY - h * 0.65f, x + w * 0.7f, baseY - h * 0.45f, x + w / 2, baseY);
                path.CloseFigure();
                using (var b = new SolidBrush(Color.FromArgb(225, color)))
                    g.FillPath(b, path);
            }
        }

        private void QueueDrone(float t)
        {
            var d = scene.Drone;
            var c = new V3(d.X, d.Y, d.Z + 0.05f);
            var yaw = Rad(scene.DroneYawDeg);
            var armed = scene.DroneArmed;
            var spinning = armed && d.Z > 0.02f;

            sorted.Add(new KeyValuePair<float, Action<Graphics>>(Depth(c), gr =>
            {
                var arm = 0.3f * DroneScale;
                var rotor = 0.15f * DroneScale;
                var rim = armed ? MissionPalette.Accent : MissionPalette.TextDim;

                PointF centre;
                if (!Project(c, out centre))
                    return;

                for (var i = 0; i < 4; i++)
                {
                    var a = yaw + Rad(45 + i * 90);
                    var end = c + new V3((float)Math.Sin(a), (float)Math.Cos(a), 0) * arm;
                    PointF endS;
                    if (!Project(end, out endS))
                        continue;

                    using (var armPen = new Pen(Color.FromArgb(215, 222, 238), 2.4f))
                        gr.DrawLine(armPen, centre, endS);

                    var disc = Circle(end.X, end.Y, end.Z + 0.02f, rotor, 18);
                    if (disc != null)
                    {
                        using (var fill = new SolidBrush(Color.FromArgb(spinning ? 70 : 40, rim)))
                        using (var pen = new Pen(Color.FromArgb(200, rim), 1.2f))
                        {
                            gr.FillPolygon(fill, disc);
                            gr.DrawPolygon(pen, disc);
                        }
                    }

                    // Blade: a fast-turning line, stationary when disarmed.
                    var spin = spinning ? t * 26f + i : i * 0.8f;
                    PointF b1, b2;
                    if (Project(end + new V3((float)Math.Cos(spin), (float)Math.Sin(spin), 0.02f) * rotor, out b1) &&
                        Project(end - new V3((float)Math.Cos(spin), (float)Math.Sin(spin), -0.02f) * rotor, out b2))
                    {
                        using (var blade = new Pen(Color.FromArgb(170, 235, 240, 250), 1.2f))
                            gr.DrawLine(blade, b1, b2);
                    }
                }

                var bodyR = Math.Max(3.5f, focal * 0.075f * DroneScale / Math.Max(1f, Depth(c)));
                using (var body = new SolidBrush(Color.FromArgb(236, 240, 250)))
                    gr.FillEllipse(body, centre.X - bodyR, centre.Y - bodyR, bodyR * 2, bodyR * 2);

                // Nose marker so heading reads at a glance.
                PointF nose;
                if (Project(c + new V3((float)Math.Sin(yaw), (float)Math.Cos(yaw), 0) * (arm * 0.75f), out nose))
                {
                    using (var pen = new Pen(MissionPalette.Accent, 2.6f) { EndCap = LineCap.Round })
                        gr.DrawLine(pen, centre, nose);
                }
            }));
        }

        #endregion

        #region hud

        private void DrawHud(Graphics g)
        {
            TextRenderer.DrawText(g, "3D SIMULATION", MissionPalette.Caps, new Point(16, 14), MissionPalette.TextDim,
                TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Subtitle, MissionPalette.Small, new Point(16, 30), MissionPalette.TextFaint,
                TextFormatFlags.NoPadding);

            DrawLegend(g);
            DrawCompass(g);

            TextRenderer.DrawText(g, "Drag to orbit  ·  right-drag to pan  ·  wheel to zoom  ·  double-click to reset",
                MissionPalette.Small, new Point(16, Height - 22), MissionPalette.TextFaint, TextFormatFlags.NoPadding);

            if (scene == null || (!scene.HasDrone && scene.Walls.Count == 0))
            {
                var size = TextRenderer.MeasureText(g, EmptyMessage, MissionPalette.Body);
                var r = new RectangleF((Width - size.Width) / 2f - 14, Height / 2f - 18, size.Width + 28, 36);
                using (var path = MissionPalette.RoundedRect(r, 8))
                using (var bg = new SolidBrush(Color.FromArgb(215, MissionPalette.Surface)))
                using (var border = new Pen(MissionPalette.Border))
                {
                    g.FillPath(bg, path);
                    g.DrawPath(border, path);
                }
                TextRenderer.DrawText(g, EmptyMessage, MissionPalette.Body, Rectangle.Round(r), MissionPalette.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void DrawLegend(Graphics g)
        {
            var items = new[]
            {
                new KeyValuePair<string, Color>("Drone", MissionPalette.Text),
                new KeyValuePair<string, Color>("Survivor", MissionPalette.Survivor),
                new KeyValuePair<string, Color>("Hazard", MissionPalette.Warning),
                new KeyValuePair<string, Color>("Rescue route", MissionPalette.Route),
                new KeyValuePair<string, Color>("Flown path", MissionPalette.Accent)
            };

            var x = Width - 16;
            for (var i = items.Length - 1; i >= 0; i--)
            {
                var size = TextRenderer.MeasureText(g, items[i].Key, MissionPalette.Small, Size.Empty, TextFormatFlags.NoPadding);
                x -= size.Width;
                TextRenderer.DrawText(g, items[i].Key, MissionPalette.Small, new Point(x, 16), MissionPalette.TextDim,
                    TextFormatFlags.NoPadding);
                x -= 12;
                using (var b = new SolidBrush(items[i].Value))
                    g.FillEllipse(b, x, 19, 7, 7);
                x -= 16;
            }
        }

        private void DrawCompass(Graphics g)
        {
            PointF c, n;
            var t = new V3(TargetX, TargetY, 0);
            if (!Project(t, out c) || !Project(t + new V3(0, 1, 0), out n))
                return;

            var dx = n.X - c.X;
            var dy = n.Y - c.Y;
            var len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-3f)
                return;
            dx /= len;
            dy /= len;

            var centre = new PointF(Width - 34, Height - 36);
            using (var ring = new Pen(MissionPalette.Border, 1.2f))
                g.DrawEllipse(ring, centre.X - 16, centre.Y - 16, 32, 32);
            using (var pen = new Pen(MissionPalette.Text, 2f) { EndCap = LineCap.ArrowAnchor })
                g.DrawLine(pen, centre.X - dx * 9, centre.Y - dy * 9, centre.X + dx * 11, centre.Y + dy * 11);
            DrawCentredText(g, "N", MissionPalette.SmallBold, new PointF(centre.X + dx * 25, centre.Y + dy * 25), MissionPalette.TextDim);
        }

        #endregion

        #region helpers

        private PointF[] Circle(float x, float y, float z, float r, int segments)
        {
            var pts = new PointF[segments];
            for (var i = 0; i < segments; i++)
            {
                var a = i * 2 * Math.PI / segments;
                if (!Project(x + r * (float)Math.Cos(a), y + r * (float)Math.Sin(a), z, out pts[i]))
                    return null;
            }
            return pts;
        }

        private void DrawLine3(Graphics g, Pen pen, V3 a, V3 b)
        {
            PointF sa, sb;
            if (Project(a, out sa) && Project(b, out sb))
                g.DrawLine(pen, sa, sb);
        }

        private void DrawFloorTag(Graphics g, V3 p, string text, Color color)
        {
            PointF s;
            if (Project(p, out s))
                DrawCentredText(g, text, MissionPalette.Caps, s, color);
        }

        private static void DrawCentredText(Graphics g, string text, Font font, PointF at, Color color)
        {
            var size = TextRenderer.MeasureText(g, text, font, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, text, font, new Point((int)(at.X - size.Width / 2f), (int)(at.Y - size.Height / 2f)),
                color, TextFormatFlags.NoPadding);
        }

        private static float Rad(float deg)
        {
            return deg * (float)Math.PI / 180f;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                frameTimer.Dispose();
            base.Dispose(disposing);
        }

        private struct V3
        {
            public readonly float X, Y, Z;

            public V3(float x, float y, float z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public static V3 operator +(V3 a, V3 b) { return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
            public static V3 operator -(V3 a, V3 b) { return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
            public static V3 operator *(V3 a, float k) { return new V3(a.X * k, a.Y * k, a.Z * k); }
            public static float Dot(V3 a, V3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }

            public static V3 Cross(V3 a, V3 b)
            {
                return new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
            }

            public V3 Normalized()
            {
                var l = (float)Math.Sqrt(X * X + Y * Y + Z * Z);
                return l < 1e-6f ? this : new V3(X / l, Y / l, Z / l);
            }
        }

        #endregion
    }
}
