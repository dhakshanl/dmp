using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// One palette and font set for every Mission page control, so the bars, rail, drawer
    /// and 3D view read as one surface. Fonts are created once: these controls repaint
    /// many times a second and a Font per paint is a GDI handle churned per frame.
    /// </summary>
    public static class MissionPalette
    {
        public static readonly Color Background = Color.FromArgb(13, 14, 20);
        public static readonly Color Surface = Color.FromArgb(21, 23, 32);
        public static readonly Color SurfaceRaised = Color.FromArgb(30, 33, 45);
        public static readonly Color Border = Color.FromArgb(44, 48, 64);
        public static readonly Color Text = Color.FromArgb(226, 230, 242);
        public static readonly Color TextDim = Color.FromArgb(142, 148, 170);
        public static readonly Color TextFaint = Color.FromArgb(92, 98, 122);

        public static readonly Color Accent = Color.FromArgb(72, 190, 255);
        public static readonly Color Survivor = Color.FromArgb(255, 190, 70);
        public static readonly Color Fire = Color.FromArgb(255, 92, 58);
        public static readonly Color Route = Color.FromArgb(90, 228, 146);
        public static readonly Color Ok = Color.FromArgb(90, 214, 134);
        public static readonly Color Warning = Color.FromArgb(255, 168, 56);
        public static readonly Color Danger = Color.FromArgb(222, 60, 60);

        public static readonly Font Small = new Font("Segoe UI", 7.75f, FontStyle.Regular);
        public static readonly Font SmallBold = new Font("Segoe UI Semibold", 7.75f, FontStyle.Regular);
        public static readonly Font Body = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font BodyBold = new Font("Segoe UI Semibold", 9f, FontStyle.Regular);
        public static readonly Font Title = new Font("Segoe UI Semibold", 10.5f, FontStyle.Regular);
        public static readonly Font Big = new Font("Segoe UI Semibold", 13f, FontStyle.Regular);
        public static readonly Font Mono = new Font("Consolas", 9f, FontStyle.Regular);

        /// <summary>Section header style: small, spaced capitals in the dim text colour.</summary>
        public static readonly Font Caps = new Font("Segoe UI Semibold", 7.5f, FontStyle.Regular);

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static string FormatClock(TimeSpan t)
        {
            return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");
        }
    }

    public enum MissionEventKind
    {
        Info,
        Survivor,
        Hazard,
        Warning,
        Critical
    }

    public class MissionEvent
    {
        public TimeSpan At;
        public string Text;
        public MissionEventKind Kind;

        public MissionEvent(TimeSpan at, MissionEventKind kind, string text)
        {
            At = at;
            Kind = kind;
            Text = text;
        }
    }

    /// <summary>
    /// Timestamped mission event feed for the telemetry drawer: detections, phase changes,
    /// arming, failsafes and autopilot/companion STATUSTEXT. Newest first; the wheel
    /// scrolls back through history.
    /// </summary>
    public class MissionEventLog : Control
    {
        private const int RowHeight = 22;
        private const int MaxEvents = 300;

        private readonly List<MissionEvent> events = new List<MissionEvent>();
        private int scroll;

        public MissionEventLog()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            BackColor = MissionPalette.Surface;
        }

        public void Add(MissionEvent e)
        {
            events.Insert(0, e);
            if (events.Count > MaxEvents)
                events.RemoveAt(events.Count - 1);

            // Keep a scrolled-back view anchored on what the reader was looking at.
            if (scroll > 0)
                scroll++;

            Invalidate();
        }

        public void Clear()
        {
            events.Clear();
            scroll = 0;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            var visible = Math.Max(1, Height / RowHeight);
            scroll = Math.Max(0, Math.Min(Math.Max(0, events.Count - visible), scroll - Math.Sign(e.Delta) * 3));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);

            if (events.Count == 0)
            {
                TextRenderer.DrawText(g, "No events yet", MissionPalette.Body,
                    new Rectangle(0, 0, Width, 40), MissionPalette.TextFaint,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            var y = 0;
            for (var i = scroll; i < events.Count && y < Height; i++, y += RowHeight)
            {
                var ev = events[i];

                TextRenderer.DrawText(g, MissionPalette.FormatClock(ev.At), MissionPalette.Mono,
                    new Rectangle(4, y, 52, RowHeight), MissionPalette.TextFaint,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                using (var dot = new SolidBrush(KindColor(ev.Kind)))
                    g.FillEllipse(dot, 60, y + RowHeight / 2 - 3, 6, 6);

                var textColor = ev.Kind == MissionEventKind.Info ? MissionPalette.TextDim : MissionPalette.Text;
                TextRenderer.DrawText(g, ev.Text, MissionPalette.Body,
                    new Rectangle(72, y, Width - 76, RowHeight), textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPadding);
            }

            if (scroll > 0)
            {
                using (var fade = new LinearGradientBrush(new Rectangle(0, 0, Width, 14),
                           Color.FromArgb(220, BackColor), Color.FromArgb(0, BackColor), 90f))
                    g.FillRectangle(fade, 0, 0, Width, 14);
            }
        }

        private static Color KindColor(MissionEventKind kind)
        {
            switch (kind)
            {
                case MissionEventKind.Survivor: return MissionPalette.Survivor;
                case MissionEventKind.Hazard: return MissionPalette.Fire;
                case MissionEventKind.Warning: return MissionPalette.Warning;
                case MissionEventKind.Critical: return MissionPalette.Danger;
                default: return MissionPalette.TextFaint;
            }
        }
    }
}
