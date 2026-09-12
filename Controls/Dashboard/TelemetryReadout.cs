using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// Two-column grid of labelled telemetry values for the Telem Log drawer - the detail
    /// the page no longer puts in front of the operator. Rows are declared once with
    /// AddRow and updated by key, so the drawer's layout never shifts as values change.
    /// </summary>
    public class TelemetryReadout : Control
    {
        private const int RowHeight = 40;
        private const int Columns = 2;

        private readonly List<string> keys = new List<string>();
        private readonly Dictionary<string, string> labels = new Dictionary<string, string>();
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();
        private readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();

        public TelemetryReadout()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = MissionPalette.Surface;
        }

        /// <summary>Height needed to show every row.</summary>
        public int PreferredHeight
        {
            get { return (keys.Count + Columns - 1) / Columns * RowHeight + 4; }
        }

        public void AddRow(string key, string label)
        {
            keys.Add(key);
            labels[key] = label;
            values[key] = "--";
        }

        public void Set(string key, string value, Color? color = null)
        {
            if (!labels.ContainsKey(key))
                return;

            values[key] = value;
            if (color.HasValue)
                colors[key] = color.Value;
            else
                colors.Remove(key);
        }

        public void ClearValues()
        {
            foreach (var k in keys)
                values[k] = "--";
            colors.Clear();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);

            var colWidth = (Width - 8) / Columns;
            for (var i = 0; i < keys.Count; i++)
            {
                var x = 4 + i % Columns * colWidth;
                var y = i / Columns * RowHeight;
                var key = keys[i];

                TextRenderer.DrawText(g, labels[key].ToUpperInvariant(), MissionPalette.Caps, new Point(x + 10, y + 4),
                    MissionPalette.TextFaint, TextFormatFlags.NoPadding);

                Color c;
                if (!colors.TryGetValue(key, out c))
                    c = values[key] == "--" ? MissionPalette.TextFaint : MissionPalette.Text;
                TextRenderer.DrawText(g, values[key], MissionPalette.BodyBold,
                    new Rectangle(x + 10, y + 18, colWidth - 14, 20), c,
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
