using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MissionPlanner.Controls
{
    // Black grid + coordinate readout + traced flight path, for GPS-denied
    // flights where the satellite map is meaningless. Position comes from
    // LOCAL_POSITION_NED (cs.posn/cs.pose - north/east in meters relative to
    // the EKF origin), which is available even without GPS as long as the
    // EKF has a relative position estimate (e.g. from optical flow).
    public class GpsDeniedGridControl : UserControl
    {
        private readonly List<PointF> trace = new List<PointF>(); // (east, north) meters
        private float currentEast;
        private float currentNorth;
        private float currentHeadingDeg;
        private bool hasPosition;

        private const float MinHalfRangeM = 2.0f;   // never zoom in tighter than a 4m x 4m view
        private const float MarginFactor = 1.25f;    // headroom around the path extents

        public GpsDeniedGridControl()
        {
            DoubleBuffered = true;
            BackColor = Color.Black;
        }

        public void AddPoint(float northM, float eastM, float headingDeg)
        {
            currentNorth = northM;
            currentEast = eastM;
            currentHeadingDeg = headingDeg;
            hasPosition = true;

            if (trace.Count == 0 || Distance(trace[trace.Count - 1], new PointF(eastM, northM)) > 0.05f)
            {
                trace.Add(new PointF(eastM, northM));
            }

            Invalidate();
        }

        public void ClearTrace()
        {
            trace.Clear();
            hasPosition = false;
            Invalidate();
        }

        private static float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // intentionally empty - OnPaint fills the whole control, avoids flicker
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Black);

            if (Width <= 0 || Height <= 0)
                return;

            // ---- work out world-space extents to show, centered on the path ----
            float minE = hasPosition ? currentEast : 0f;
            float maxE = minE;
            float minN = hasPosition ? currentNorth : 0f;
            float maxN = minN;
            foreach (var p in trace)
            {
                minE = Math.Min(minE, p.X); maxE = Math.Max(maxE, p.X);
                minN = Math.Min(minN, p.Y); maxN = Math.Max(maxN, p.Y);
            }

            float centerE = (minE + maxE) / 2f;
            float centerN = (minN + maxN) / 2f;
            float halfRangeE = Math.Max(MinHalfRangeM, (maxE - minE) / 2f * MarginFactor);
            float halfRangeN = Math.Max(MinHalfRangeM, (maxN - minN) / 2f * MarginFactor);

            // single uniform scale (meters -> pixels) so the grid stays square
            float scale = Math.Min(Width / (2f * halfRangeE), Height / (2f * halfRangeN));
            if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale))
                scale = 20f;

            PointF ToScreen(float eastM, float northM)
            {
                float x = (Width / 2f) + (eastM - centerE) * scale;
                float y = (Height / 2f) - (northM - centerN) * scale; // screen Y grows downward
                return new PointF(x, y);
            }

            DrawGrid(g, centerE, centerN, scale);

            // ---- path trace ----
            if (trace.Count >= 2)
            {
                var pts = new PointF[trace.Count];
                for (int i = 0; i < trace.Count; i++)
                    pts[i] = ToScreen(trace[i].X, trace[i].Y);
                using (var tracePen = new Pen(Color.Lime, 2f))
                    g.DrawLines(tracePen, pts);
            }

            // ---- current position marker (triangle pointing along heading) ----
            if (hasPosition)
            {
                var center = ToScreen(currentEast, currentNorth);
                DrawHeadingMarker(g, center, currentHeadingDeg);

                using (var font = new Font("Consolas", 9f))
                using (var brush = new SolidBrush(Color.White))
                {
                    string label = string.Format("N {0,7:0.00}m   E {1,7:0.00}m", currentNorth, currentEast);
                    g.DrawString(label, font, brush, 6, 6);
                }
            }
        }

        private void DrawGrid(Graphics g, float centerE, float centerN, float scale)
        {
            float spacing = NiceGridSpacing(Math.Max(Width, Height) / scale / 6f);

            using (var gridPen = new Pen(Color.FromArgb(60, 60, 60)))
            using (var axisPen = new Pen(Color.FromArgb(110, 110, 110)))
            using (var font = new Font("Consolas", 7.5f))
            using (var textBrush = new SolidBrush(Color.FromArgb(160, 160, 160)))
            {
                float worldLeft = centerE - (Width / 2f) / scale;
                float worldRight = centerE + (Width / 2f) / scale;
                float worldTop = centerN + (Height / 2f) / scale;
                float worldBottom = centerN - (Height / 2f) / scale;

                float startE = (float)Math.Floor(worldLeft / spacing) * spacing;
                for (float e = startE; e <= worldRight; e += spacing)
                {
                    float x = (Width / 2f) + (e - centerE) * scale;
                    bool isAxis = Math.Abs(e) < spacing / 2f;
                    g.DrawLine(isAxis ? axisPen : gridPen, x, 0, x, Height);
                    g.DrawString(e.ToString("0.#"), font, textBrush, x + 2, Height - 16);
                }

                float startN = (float)Math.Floor(worldBottom / spacing) * spacing;
                for (float n = startN; n <= worldTop; n += spacing)
                {
                    float y = (Height / 2f) - (n - centerN) * scale;
                    bool isAxis = Math.Abs(n) < spacing / 2f;
                    g.DrawLine(isAxis ? axisPen : gridPen, 0, y, Width, y);
                    g.DrawString(n.ToString("0.#"), font, textBrush, 2, y + 2);
                }
            }
        }

        // pick a "round" grid spacing (1, 2, 5, 10, 20, 50... meters) near the target
        private static float NiceGridSpacing(float target)
        {
            if (target <= 0 || float.IsNaN(target))
                return 1f;
            float exp = (float)Math.Floor(Math.Log10(target));
            float baseVal = (float)Math.Pow(10, exp);
            float[] steps = { 1f, 2f, 5f, 10f };
            float best = baseVal;
            foreach (var s in steps)
            {
                float candidate = s * baseVal;
                if (candidate <= target)
                    best = candidate;
            }
            return best;
        }

        private static void DrawHeadingMarker(Graphics g, PointF center, float headingDeg)
        {
            const float size = 9f;
            var pts = new[]
            {
                new PointF(0, -size),
                new PointF(size * 0.6f, size * 0.7f),
                new PointF(-size * 0.6f, size * 0.7f),
            };

            double rad = headingDeg * Math.PI / 180.0;
            float cos = (float)Math.Cos(rad);
            float sin = (float)Math.Sin(rad);
            for (int i = 0; i < pts.Length; i++)
            {
                float x = pts[i].X * cos - pts[i].Y * sin;
                float y = pts[i].X * sin + pts[i].Y * cos;
                pts[i] = new PointF(center.X + x, center.Y + y);
            }

            using (var brush = new SolidBrush(Color.OrangeRed))
                g.FillPolygon(brush, pts);
        }
    }
}
