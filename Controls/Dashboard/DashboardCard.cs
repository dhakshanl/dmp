using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// Plain, self-drawn rounded dark "card" panel used as the background for every
    /// dashboard section (battery, telemetry, log manager, connection health, mission
    /// control). Deliberately implemented with GDI+ rather than a vendored Guna2 shadow
    /// panel: this codebase already proves out Guna2CircleProgressBar/HtmlLabel/TextBox/
    /// VProgressBar (GCSViews\Help.Designer.cs), but no shadow/gradient panel type is used
    /// anywhere in this repo's own source, so hand-drawing a known-good rounded panel here
    /// avoids guessing at an unverified third-party API surface.
    /// </summary>
    public class DashboardCard : Panel
    {
        public Color CardColor { get; set; } = Color.FromArgb(24, 24, 38);
        public Color BorderColor { get; set; } = Color.FromArgb(50, 50, 72);
        public int CornerRadius { get; set; } = 10;

        public DashboardCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(18, 18, 30);
            Padding = new Padding(12);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = RoundedRect(rect, CornerRadius))
            using (var fill = new SolidBrush(CardColor))
            using (var border = new Pen(BorderColor, 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
        }

        private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
