using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>What the status bar shows. Nullable/negative fields mean "no data" and render as a dash.</summary>
    public class MissionStatus
    {
        /// <summary>MAVLink GPS fix type; below 3 is treated as GPS-denied.</summary>
        public int GpsFix = -1;
        public int Satellites;

        /// <summary>What the autopilot is navigating on while GPS-denied, e.g. "ZED VIO". Optional.</summary>
        public string NavSource;

        public string Mode;
        public bool? Armed;

        public int BatteryPercent = -1;
        public double BatteryVolts = -1;

        /// <summary>Time since takeoff; null while the mission has not started.</summary>
        public TimeSpan? MissionTime;

        public int SurvivorsFound = -1;
        public float ExploredFraction = -1;
    }

    /// <summary>
    /// The page's bottom instrument strip: navigation source, mode, mission clock against
    /// the 30-minute cap, survivors found, area explored, arming state and battery - the
    /// handful of numbers an operator glances at, with everything else in the drawer.
    /// Custom-painted so ThemeManager (which restyles stock Labels and Panels) leaves it alone.
    /// </summary>
    public class MissionStatusBar : Control
    {
        public static readonly TimeSpan MissionCap = TimeSpan.FromMinutes(30);

        private MissionStatus status = new MissionStatus();

        public MissionStatusBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = MissionPalette.Surface;
            Height = 50;
        }

        public void SetStatus(MissionStatus s)
        {
            status = s ?? new MissionStatus();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            using (var top = new Pen(MissionPalette.Border))
                g.DrawLine(top, 0, 0, Width, 0);

            // Right-hand group first, so the left segments know where they must stop.
            var right = Width - 12;
            right = DrawBattery(g, right);
            right = DrawArmed(g, right);

            var x = 12;
            x = DrawNavigation(g, x, right);
            x = DrawSegment(g, x, right, 118, "MODE", string.IsNullOrEmpty(status.Mode) ? "--" : status.Mode.ToUpperInvariant(), MissionPalette.Text);
            x = DrawMissionClock(g, x, right);
            x = DrawSegment(g, x, right, 104, "SURVIVORS",
                status.SurvivorsFound < 0 ? "--" : status.SurvivorsFound + " / " + MissionScene.MaxSurvivors,
                status.SurvivorsFound > 0 ? MissionPalette.Survivor : MissionPalette.Text);
            DrawSegment(g, x, right, 100, "EXPLORED",
                status.ExploredFraction < 0 ? "--" : (status.ExploredFraction * 100).ToString("0") + " %",
                MissionPalette.Text);
        }

        private int DrawNavigation(Graphics g, int x, int limit)
        {
            const int width = 196;
            if (x + width > limit)
                return x;

            string value;
            Color dot;
            if (status.GpsFix < 0)
            {
                value = "--";
                dot = MissionPalette.TextFaint;
            }
            else if (status.GpsFix >= 3)
            {
                value = "GPS " + (status.GpsFix >= 4 ? "RTK" : "3D") + " · " + status.Satellites + " SATS";
                dot = MissionPalette.Ok;
            }
            else
            {
                value = string.IsNullOrEmpty(status.NavSource) ? "GPS DENIED" : "GPS DENIED · " + status.NavSource;
                dot = MissionPalette.Warning;
            }

            DrawCaps(g, "NAVIGATION", x + 14, 8);
            using (var b = new SolidBrush(dot))
                g.FillEllipse(b, x, 29, 8, 8);
            TextRenderer.DrawText(g, value, MissionPalette.BodyBold, new Point(x + 12, 24), MissionPalette.Text,
                TextFormatFlags.NoPadding);

            Divider(g, x + width - 12);
            return x + width;
        }

        private int DrawMissionClock(Graphics g, int x, int limit)
        {
            const int width = 150;
            if (x + width > limit)
                return x;

            DrawCaps(g, "MISSION", x, 8);

            if (!status.MissionTime.HasValue)
            {
                TextRenderer.DrawText(g, "-- / 30:00", MissionPalette.BodyBold, new Point(x, 24),
                    MissionPalette.TextDim, TextFormatFlags.NoPadding);
            }
            else
            {
                var t = status.MissionTime.Value;
                var frac = Math.Min(1.0, t.TotalSeconds / MissionCap.TotalSeconds);
                var color = frac > 0.93 ? MissionPalette.Danger : frac > 0.83 ? MissionPalette.Warning : MissionPalette.Text;

                TextRenderer.DrawText(g, MissionPalette.FormatClock(t) + " / 30:00", MissionPalette.BodyBold,
                    new Point(x, 24), color, TextFormatFlags.NoPadding);

                // Thin cap gauge: how much of the 30-minute allowance is gone.
                var barW = width - 26;
                using (var track = new SolidBrush(MissionPalette.SurfaceRaised))
                    g.FillRectangle(track, x, 42, barW, 2);
                using (var fill = new SolidBrush(frac > 0.83 ? color : MissionPalette.Accent))
                    g.FillRectangle(fill, x, 42, (float)(barW * frac), 2);
            }

            Divider(g, x + width - 12);
            return x + width;
        }

        private int DrawSegment(Graphics g, int x, int limit, int width, string caps, string value, Color valueColor)
        {
            if (x + width > limit)
                return x;

            DrawCaps(g, caps, x, 8);
            TextRenderer.DrawText(g, value, MissionPalette.BodyBold, new Point(x, 24), valueColor, TextFormatFlags.NoPadding);
            Divider(g, x + width - 12);
            return x + width;
        }

        private int DrawArmed(Graphics g, int right)
        {
            const int w = 96, h = 26;
            var r = new RectangleF(right - w, (Height - h) / 2f, w, h);

            using (var path = MissionPalette.RoundedRect(r, 5))
            {
                if (status.Armed == true)
                {
                    using (var fill = new SolidBrush(MissionPalette.Danger))
                        g.FillPath(fill, path);
                }
                else
                {
                    using (var pen = new Pen(MissionPalette.Border, 1.2f))
                        g.DrawPath(pen, path);
                }
            }

            var text = status.Armed == null ? "--" : status.Armed.Value ? "ARMED" : "DISARMED";
            var color = status.Armed == true ? Color.White : MissionPalette.TextDim;
            TextRenderer.DrawText(g, text, MissionPalette.BodyBold, Rectangle.Round(r), color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            return right - w - 18;
        }

        private int DrawBattery(Graphics g, int right)
        {
            const int width = 176;
            var x = right - width;

            DrawCaps(g, "BATTERY", x, 8);

            if (status.BatteryPercent < 0)
            {
                TextRenderer.DrawText(g, "--", MissionPalette.BodyBold, new Point(x, 24), MissionPalette.TextDim,
                    TextFormatFlags.NoPadding);
                return x - 14;
            }

            var pct = Math.Max(0, Math.Min(100, status.BatteryPercent));
            var color = pct <= 20 ? MissionPalette.Danger : pct <= 40 ? MissionPalette.Warning : MissionPalette.Ok;

            var text = pct + "%";
            if (status.BatteryVolts > 0)
                text += "   " + status.BatteryVolts.ToString("0.00") + " V";
            TextRenderer.DrawText(g, text, MissionPalette.BodyBold, new Point(x, 24), MissionPalette.Text,
                TextFormatFlags.NoPadding);

            // Battery glyph on the right edge: body, terminal nub, fill by charge.
            var body = new RectangleF(right - 40, 26, 34, 14);
            using (var pen = new Pen(MissionPalette.TextDim, 1.2f))
                g.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
            using (var nub = new SolidBrush(MissionPalette.TextDim))
                g.FillRectangle(nub, body.Right, body.Y + 4, 3, body.Height - 8);
            using (var fill = new SolidBrush(color))
                g.FillRectangle(fill, body.X + 2, body.Y + 2, (body.Width - 4) * pct / 100f, body.Height - 4);

            return x - 14;
        }

        private static void DrawCaps(Graphics g, string text, int x, int y)
        {
            TextRenderer.DrawText(g, text, MissionPalette.Caps, new Point(x, y), MissionPalette.TextFaint,
                TextFormatFlags.NoPadding);
        }

        private void Divider(Graphics g, int x)
        {
            using (var pen = new Pen(MissionPalette.Border))
                g.DrawLine(pen, x, 10, x, Height - 10);
        }
    }
}
