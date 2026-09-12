using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// Rounded, self-painted button for the Mission page. A Control rather than a Button
    /// subclass so its fill, hover and toggled states are fully ours - ThemeManager restyles
    /// stock Buttons, which would strip Abort of its red.
    /// </summary>
    public class MissionActionButton : Control
    {
        private bool hover;
        private bool pressed;

        public Color FillColor { get; set; } = MissionPalette.SurfaceRaised;
        public Color TextColor { get; set; } = MissionPalette.Text;

        /// <summary>Draws a border instead of a fill - for secondary actions.</summary>
        public bool Outline { get; set; }

        /// <summary>Latched state for toggles (drawer, demo): accent border and text.</summary>
        public bool Toggled { get; set; }

        /// <summary>Optional status dot before the text, e.g. link state on Connect.</summary>
        public Color? DotColor { get; set; }

        public MissionActionButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = MissionPalette.BodyBold;
            Cursor = Cursors.Hand;
            Size = new Size(110, 32);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            pressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            pressed = false;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = MissionPalette.RoundedRect(r, 6))
            {
                if (Outline)
                {
                    var border = Toggled ? MissionPalette.Accent : hover && Enabled ? MissionPalette.TextFaint : MissionPalette.Border;
                    if (Toggled)
                    {
                        using (var tint = new SolidBrush(Color.FromArgb(28, MissionPalette.Accent)))
                            g.FillPath(tint, path);
                    }
                    using (var pen = new Pen(border, 1.2f))
                        g.DrawPath(pen, path);
                }
                else
                {
                    var fill = !Enabled ? MissionPalette.SurfaceRaised
                        : pressed ? ControlPaint.Dark(FillColor, 0.1f)
                        : hover ? ControlPaint.Light(FillColor, 0.18f)
                        : FillColor;
                    using (var b = new SolidBrush(fill))
                        g.FillPath(b, path);
                }
            }

            var textColor = !Enabled ? MissionPalette.TextFaint : Toggled ? MissionPalette.Accent : TextColor;
            var textSize = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            var dotSpace = DotColor.HasValue ? 14 : 0;
            var x = (Width - textSize.Width - dotSpace) / 2;

            if (DotColor.HasValue)
            {
                using (var dot = new SolidBrush(DotColor.Value))
                    g.FillEllipse(dot, x, Height / 2f - 4, 8, 8);
            }

            TextRenderer.DrawText(g, Text, Font, new Point(x + dotSpace, (Height - textSize.Height) / 2), textColor,
                TextFormatFlags.NoPadding);
        }
    }

    /// <summary>
    /// The page's top bar: mission name and live phase on the left, a right-aligned row of
    /// action buttons (laid out here, so no themed FlowLayoutPanel is needed).
    /// </summary>
    public class MissionHeader : Control
    {
        private readonly List<Control> rightControls = new List<Control>();
        private string phase = "";
        private Color phaseColor = MissionPalette.TextDim;

        public string Title { get; set; } = "AirMouse Mission";

        public MissionHeader()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = MissionPalette.Surface;
            Height = 56;
        }

        public void SetPhase(string text, Color color)
        {
            if (phase == text && phaseColor == color)
                return;
            phase = text ?? "";
            phaseColor = color;
            Invalidate();
        }

        /// <summary>Adds a control to the right-hand group; the first added sits furthest right.</summary>
        public void AddRight(Control c)
        {
            rightControls.Add(c);
            Controls.Add(c);
            PerformLayout();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var x = Width - 16;
            foreach (var c in rightControls)
            {
                if (!c.Visible)
                    continue;
                x -= c.Width;
                c.Location = new Point(x, (Height - c.Height) / 2);
                x -= 8;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            using (var pen = new Pen(MissionPalette.Border))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);

            TextRenderer.DrawText(g, Title, MissionPalette.Title, new Point(18, 10), MissionPalette.Text,
                TextFormatFlags.NoPadding);

            using (var dot = new SolidBrush(phaseColor))
                g.FillEllipse(dot, 19, 36, 7, 7);
            TextRenderer.DrawText(g, phase, MissionPalette.Small, new Point(32, 33), MissionPalette.TextDim,
                TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Small spaced-capitals section title with an optional right-aligned note.</summary>
    public class SectionHeader : Control
    {
        private string note = "";

        public SectionHeader(string text)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = text;
            BackColor = MissionPalette.Surface;
            Height = 30;
        }

        public string Note
        {
            get { return note; }
            set
            {
                if (note == value)
                    return;
                note = value ?? "";
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            TextRenderer.DrawText(g, Text.ToUpperInvariant(), MissionPalette.Caps, new Point(14, 12),
                MissionPalette.TextDim, TextFormatFlags.NoPadding);

            if (note.Length > 0)
            {
                var size = TextRenderer.MeasureText(g, note, MissionPalette.Small, Size.Empty, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, note, MissionPalette.Small, new Point(Width - 14 - size.Width, 11),
                    MissionPalette.TextFaint, TextFormatFlags.NoPadding);
            }
        }
    }

    /// <summary>Small self-painted circular progress ring, used for ULog replay position in the drawer.</summary>
    public class MissionProgressRing : Control
    {
        private int val;

        public int Value
        {
            get { return val; }
            set
            {
                var clamped = Math.Max(0, Math.Min(100, value));
                if (clamped == val)
                    return;
                val = clamped;
                Invalidate();
            }
        }

        public Color RingColor { get; set; } = MissionPalette.Accent;

        public MissionProgressRing()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(36, 36);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            const float thickness = 4f;
            var r = new RectangleF(thickness / 2, thickness / 2, Width - thickness, Height - thickness);

            using (var track = new Pen(MissionPalette.Border, thickness))
                g.DrawEllipse(track, r);

            if (val > 0)
            {
                using (var pen = new Pen(RingColor, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(pen, r, -90, val * 3.6f);
            }

            var text = val + "%";
            var size = TextRenderer.MeasureText(g, text, MissionPalette.Small, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, text, MissionPalette.Small,
                new Point((int)(Width - size.Width) / 2, (int)(Height - size.Height) / 2),
                MissionPalette.TextDim, TextFormatFlags.NoPadding);
        }
    }

    [Flags]
    public enum Edges
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8
    }

    /// <summary>
    /// Flat surface container for the left rail and the drawer, with hairline borders on
    /// chosen edges. A Panel subclass - ThemeManager only matches Panel exactly.
    /// </summary>
    public class MissionSurface : Panel
    {
        public Edges BorderEdges { get; set; }

        public MissionSurface()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = MissionPalette.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            using (var pen = new Pen(MissionPalette.Border))
            {
                if ((BorderEdges & Edges.Left) != 0) g.DrawLine(pen, 0, 0, 0, Height);
                if ((BorderEdges & Edges.Right) != 0) g.DrawLine(pen, Width - 1, 0, Width - 1, Height);
                if ((BorderEdges & Edges.Top) != 0) g.DrawLine(pen, 0, 0, Width, 0);
                if ((BorderEdges & Edges.Bottom) != 0) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
        }
    }
}
