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
            BrightIdeasSoftware.CellStyle cellStyle1 = new BrightIdeasSoftware.CellStyle();
            BrightIdeasSoftware.CellStyle cellStyle2 = new BrightIdeasSoftware.CellStyle();
            BrightIdeasSoftware.CellStyle cellStyle3 = new BrightIdeasSoftware.CellStyle();
            this.button1 = new System.Windows.Forms.Button();
            this.Tab1 = new System.Windows.Forms.TabPage();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.panel2 = new System.Windows.Forms.Panel();
            this.hyperlinkStyle1 = new BrightIdeasSoftware.HyperlinkStyle();
            this.ucVideoStream1 = new MissionPlanner.UcVideoStream();
            this.Tab1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.BackgroundImage = global::MissionPlanner.Properties.Resources.camera_icon_G;
            this.button1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.button1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.button1.Location = new System.Drawing.Point(5, 3);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(41, 40);
            this.button1.TabIndex = 9;
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // Tab1
            // 
            this.Tab1.Controls.Add(this.ucVideoStream1);
            this.Tab1.Location = new System.Drawing.Point(4, 22);
            this.Tab1.Name = "Tab1";
            this.Tab1.Padding = new System.Windows.Forms.Padding(3);
            this.Tab1.Size = new System.Drawing.Size(430, 368);
            this.Tab1.TabIndex = 0;
            this.Tab1.Text = "Tab1";
            this.Tab1.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.Tab1);
            this.tabControl1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.tabControl1.Location = new System.Drawing.Point(48, 3);
            this.tabControl1.Multiline = true;
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(438, 394);
            this.tabControl1.SizeMode = System.Windows.Forms.TabSizeMode.FillToRight;
            this.tabControl1.TabIndex = 8;
            // 
            // panel2
            // 
            this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panel2.Controls.Add(this.button1);
            this.panel2.Controls.Add(this.tabControl1);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Right;
            this.panel2.Location = new System.Drawing.Point(549, 0);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(489, 709);
            this.panel2.TabIndex = 11;
            // 
            // hyperlinkStyle1
            // 
            cellStyle1.Font = null;
            cellStyle1.ForeColor = System.Drawing.Color.Blue;
            this.hyperlinkStyle1.Normal = cellStyle1;
            cellStyle2.Font = null;
            cellStyle2.FontStyle = System.Drawing.FontStyle.Underline;
            this.hyperlinkStyle1.Over = cellStyle2;
            this.hyperlinkStyle1.OverCursor = System.Windows.Forms.Cursors.Hand;
            cellStyle3.Font = null;
            cellStyle3.ForeColor = System.Drawing.Color.Purple;
            this.hyperlinkStyle1.Visited = cellStyle3;
            // 
            // ucVideoStream1
            // 
            this.ucVideoStream1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ucVideoStream1.Location = new System.Drawing.Point(26, 0);
            this.ucVideoStream1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.ucVideoStream1.Name = "ucVideoStream1";
            this.ucVideoStream1.Size = new System.Drawing.Size(404, 361);
            this.ucVideoStream1.TabIndex = 0;
            this.ucVideoStream1.Load += new System.EventHandler(this.ucVideoStream1_Load);
            // 
            // Help
            // 
            this.Controls.Add(this.panel2);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Name = "Help";
            this.Size = new System.Drawing.Size(1038, 709);
            this.Tab1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TabPage Tab1;
        private System.Windows.Forms.TabControl tabControl1;
        private UcVideoStream ucVideoStream1;
        private System.Windows.Forms.Panel panel2;
        private BrightIdeasSoftware.HyperlinkStyle hyperlinkStyle1;
    }
}
