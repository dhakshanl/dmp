namespace MissionPlanner.GCSViews
{
    partial class Help
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            BrightIdeasSoftware.CellStyle cellStyle7 = new BrightIdeasSoftware.CellStyle();
            BrightIdeasSoftware.CellStyle cellStyle8 = new BrightIdeasSoftware.CellStyle();
            BrightIdeasSoftware.CellStyle cellStyle9 = new BrightIdeasSoftware.CellStyle();
            this.button1 = new System.Windows.Forms.Button();
            this.Tab1 = new System.Windows.Forms.TabPage();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.ucVideoStream1 = new MissionPlanner.UcVideoStream();
            this.transparentPanel1 = new MissionPlanner.Controls.TransparentPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.hyperlinkStyle1 = new BrightIdeasSoftware.HyperlinkStyle();
            this.panel2 = new System.Windows.Forms.Panel();
            this.Tab1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.transparentPanel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.BackgroundImage = global::MissionPlanner.Properties.Resources.camera_icon_G;
            this.button1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.button1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.button1.Location = new System.Drawing.Point(71, 0);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(41, 40);
            this.button1.TabIndex = 9;
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // tabPage1
            // 
            this.Tab1.Controls.Add(this.ucVideoStream1);
            this.Tab1.Location = new System.Drawing.Point(4, 29);
            this.Tab1.Name = "Tab1";
            this.Tab1.Padding = new System.Windows.Forms.Padding(3);
            this.Tab1.Size = new System.Drawing.Size(430, 361);
            this.Tab1.TabIndex = 0;
            this.Tab1.Text = "Tab1";
            this.Tab1.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.Tab1);
            this.tabControl1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.tabControl1.Location = new System.Drawing.Point(114, 5);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(438, 394);
            this.tabControl1.TabIndex = 8;
            // 
            // ucVideoStream1
            // 
            this.ucVideoStream1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ucVideoStream1.Location = new System.Drawing.Point(3, 3);
            this.ucVideoStream1.Name = "ucVideoStream1";
            this.ucVideoStream1.Size = new System.Drawing.Size(424, 355);
            this.ucVideoStream1.TabIndex = 0;
            // 
            // transparentPanel1
            // 
            this.transparentPanel1.AutoSize = true;
            this.transparentPanel1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.transparentPanel1.Controls.Add(this.panel2);
            this.transparentPanel1.Controls.Add(this.panel1);
            this.transparentPanel1.Dock = System.Windows.Forms.DockStyle.Right;
            this.transparentPanel1.Location = new System.Drawing.Point(484, 0);
            this.transparentPanel1.Name = "transparentPanel1";
            this.transparentPanel1.Size = new System.Drawing.Size(554, 709);
            this.transparentPanel1.TabIndex = 10;
            // 
            // panel1
            // 
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.ForeColor = System.Drawing.SystemColors.ControlText;
            this.panel1.Location = new System.Drawing.Point(0, 404);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(550, 301);
            this.panel1.TabIndex = 10;
            // 
            // hyperlinkStyle1
            // 
            cellStyle7.Font = null;
            cellStyle7.ForeColor = System.Drawing.Color.Blue;
            this.hyperlinkStyle1.Normal = cellStyle7;
            cellStyle8.Font = null;
            cellStyle8.FontStyle = System.Drawing.FontStyle.Underline;
            this.hyperlinkStyle1.Over = cellStyle8;
            this.hyperlinkStyle1.OverCursor = System.Windows.Forms.Cursors.Hand;
            cellStyle9.Font = null;
            cellStyle9.ForeColor = System.Drawing.Color.Purple;
            this.hyperlinkStyle1.Visited = cellStyle9;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.tabControl1);
            this.panel2.Controls.Add(this.button1);
            this.panel2.Location = new System.Drawing.Point(16, -2);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(531, 402);
            this.panel2.TabIndex = 11;
            // 
            // Help
            // 
            this.Controls.Add(this.transparentPanel1);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Name = "Help";
            this.Size = new System.Drawing.Size(1038, 709);
            this.Load += new System.EventHandler(this.Help_Load);
            this.Tab1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.transparentPanel1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TabPage Tab1;
        private System.Windows.Forms.TabControl tabControl1;
        private UcVideoStream ucVideoStream1;
        private Controls.TransparentPanel transparentPanel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel1;
        private BrightIdeasSoftware.HyperlinkStyle hyperlinkStyle1;
    }
}
