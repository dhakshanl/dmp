using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MissionPlanner
{
    public partial class batteryInfo : UserControl
    {
        private int _progressValue = 65;
        public batteryInfo()
        {
            InitializeComponent();
           
            this.DoubleBuffered = true; // Essential for "fancy" graphics to prevent flickering
            this.BackColor = Color.FromArgb(32, 32, 32); // Modern dark theme background
            this.Size = new Size(300, 150); 
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias; // Makes the edges of your elements smooth

            // 1. Draw a Fancy Neon Border around the whole control
            using (Pen neonPen = new Pen(Color.Cyan, 2))
            {
                g.DrawRectangle(neonPen, 1, 1, this.Width - 3, this.Height - 3);
            }

            // 2. DRAW THE CUSTOM PROGRESS BAR
            Rectangle barRect = new Rectangle(20, 50, 250, 20);

            // Draw the Background (the "Empty" part)
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(60, 60, 60)))
            {
                g.FillRectangle(bgBrush, barRect);
            }

            // Calculate the width based on progress percentage
            int fillWidth = (barRect.Width * _progressValue) / 100;
            Rectangle fillRect = new Rectangle(barRect.X, barRect.Y, fillWidth, barRect.Height);

            // Draw the "Fancy" Fill with a Gradient (Neon Blue to Cyan)
            if (fillWidth > 0)
            {
                using (LinearGradientBrush lgb = new LinearGradientBrush(fillRect, Color.DeepSkyBlue, Color.Cyan, 0f))
                {
                    g.FillRectangle(lgb, fillRect);
                }
            }

            // 3. Draw some "Fancy" text status
            string statusText = $"System Loading: {_progressValue}%";
            g.DrawString(statusText, new Font("Segoe UI", 10, FontStyle.Bold), Brushes.White, 20, 25);
        }

        private void guna2CircleButton1_Click(object sender, EventArgs e)
        {

        }

        private void guna2CheckBox1_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void guna2DataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
    }
}
