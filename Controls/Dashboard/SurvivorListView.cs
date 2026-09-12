using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// Left-rail list of everything the drone has located: each survivor with its grid box
    /// (what the brief asks the GCS to report), when it was found and the length of its
    /// safe rescue route, then known fire hazards. Clicking a survivor selects the route the
    /// 3D view highlights. Reads the shared MissionScene directly; call Invalidate after
    /// the scene changes.
    /// </summary>
    public class SurvivorListView : Control
    {
        private const int HeaderHeight = 44;
        private const int SurvivorRowHeight = 46;
        private const int HazardRowHeight = 30;

        private MissionScene scene;
        private readonly List<KeyValuePair<Rectangle, int>> hitRows = new List<KeyValuePair<Rectangle, int>>();
        private int hoverId;

        /// <summary>Raised with the survivor id the operator clicked.</summary>
        public event Action<int> SurvivorSelected;

        public SurvivorListView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = MissionPalette.Surface;
        }

        public void Bind(MissionScene s)
        {
            scene = s;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var id = HitTest(e.Location);
            if (id != hoverId)
            {
                hoverId = id;
                Cursor = id != 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverId = 0;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            var id = HitTest(e.Location);
            if (id != 0)
                SurvivorSelected?.Invoke(id);
        }

        private int HitTest(Point p)
        {
            foreach (var row in hitRows)
            {
                if (row.Key.Contains(p))
                    return row.Value;
            }
            return 0;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            hitRows.Clear();

            var found = scene?.Survivors.Count ?? 0;

            TextRenderer.DrawText(g, "SURVIVORS FOUND", MissionPalette.Caps, new Point(14, 16), MissionPalette.TextDim,
                TextFormatFlags.NoPadding);
            // No fixed cap here - a real site can hold any number, so this is a plain
            // running count, not a "found / N" ratio.
            var countText = found.ToString();
            var countSize = TextRenderer.MeasureText(g, countText, MissionPalette.Big, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, countText, MissionPalette.Big, new Point(Width - 14 - countSize.Width, 10),
                found > 0 ? MissionPalette.Survivor : MissionPalette.TextFaint, TextFormatFlags.NoPadding);

            var y = HeaderHeight;

            if (found == 0)
            {
                TextRenderer.DrawText(g, "None located yet.\nDetections from the onboard camera appear here with their grid box.",
                    MissionPalette.Small, new Rectangle(14, y, Width - 28, 44), MissionPalette.TextFaint,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                y += 52;
            }
            else
            {
                foreach (var s in scene.Survivors)
                {
                    DrawSurvivorRow(g, s, new Rectangle(6, y, Width - 12, SurvivorRowHeight - 4));
                    y += SurvivorRowHeight;
                }
            }

            if (scene == null || scene.Hazards.Count == 0)
                return;

            y += 8;
            TextRenderer.DrawText(g, "HAZARDS", MissionPalette.Caps, new Point(14, y), MissionPalette.TextDim,
                TextFormatFlags.NoPadding);
            y += 20;

            foreach (var h in scene.Hazards)
            {
                using (var b = new SolidBrush(HazardVisuals.Color(h.Type)))
                    g.FillEllipse(b, 16, y + HazardRowHeight / 2 - 4, 8, 8);

                TextRenderer.DrawText(g, HazardVisuals.Label(h.Type) + "  ·  grid " + h.Grid, MissionPalette.BodyBold,
                    new Rectangle(32, y, Width - 40, HazardRowHeight), MissionPalette.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                var tag = h.Priority.ToString().ToUpperInvariant();
                var tagSize = TextRenderer.MeasureText(g, tag, MissionPalette.SmallBold, Size.Empty, TextFormatFlags.NoPadding);
                var tagRect = new Rectangle(Width - 14 - tagSize.Width - 12, y + HazardRowHeight / 2 - 9, tagSize.Width + 12, 18);
                using (var path = MissionPalette.RoundedRect(tagRect, 4))
                using (var fill = new SolidBrush(Color.FromArgb(36, HazardVisuals.PriorityColor(h.Priority))))
                    g.FillPath(fill, path);
                TextRenderer.DrawText(g, tag, MissionPalette.SmallBold, tagRect, HazardVisuals.PriorityColor(h.Priority),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                y += HazardRowHeight;
            }
        }

        private void DrawSurvivorRow(Graphics g, Detection s, Rectangle r)
        {
            hitRows.Add(new KeyValuePair<Rectangle, int>(r, s.Id));

            var selected = scene.SelectedSurvivorId == s.Id;
            if (selected || hoverId == s.Id)
            {
                using (var path = MissionPalette.RoundedRect(r, 6))
                using (var fill = new SolidBrush(selected ? MissionPalette.SurfaceRaised : Color.FromArgb(26, 29, 40)))
                    g.FillPath(fill, path);
            }

            if (selected)
            {
                using (var bar = new SolidBrush(MissionPalette.Route))
                    g.FillRectangle(bar, r.X, r.Y + 8, 3, r.Height - 16);
            }

            // Badge: S-number in a survivor-amber ring.
            var badge = new RectangleF(r.X + 10, r.Y + (r.Height - 28) / 2f, 28, 28);
            using (var ring = new Pen(MissionPalette.Survivor, 1.6f))
                g.DrawEllipse(ring, badge);
            TextRenderer.DrawText(g, s.Name, MissionPalette.SmallBold, Rectangle.Round(badge), MissionPalette.Survivor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            var textX = r.X + 48;
            TextRenderer.DrawText(g, "Grid " + s.Grid, MissionPalette.BodyBold, new Point(textX, r.Y + 6),
                MissionPalette.Text, TextFormatFlags.NoPadding);

            var len = s.RouteLength;
            var detail = "found " + MissionPalette.FormatClock(s.DetectedAt) + "  ·  " +
                         (len > 0 ? "route " + len.ToString("0.0") + " m" : "no safe route yet");
            TextRenderer.DrawText(g, detail, MissionPalette.Small, new Point(textX, r.Y + 24),
                len > 0 ? MissionPalette.TextDim : MissionPalette.Warning, TextFormatFlags.NoPadding);
        }
    }
}
