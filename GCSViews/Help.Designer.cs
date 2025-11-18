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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Help));
            BrightIdeasSoftware.CellStyle cellStyle1 = new BrightIdeasSoftware.CellStyle();
            BrightIdeasSoftware.CellStyle cellStyle2 = new BrightIdeasSoftware.CellStyle();
            BrightIdeasSoftware.CellStyle cellStyle3 = new BrightIdeasSoftware.CellStyle();
            this.add_camera_button = new System.Windows.Forms.Button();
           
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.hyperlinkStyle1 = new BrightIdeasSoftware.HyperlinkStyle();
            this.splitContainer_help = new System.Windows.Forms.SplitContainer();
            this.red_close_button = new System.Windows.Forms.Button();
            this.ucVideoStream1 = new MissionPlanner.UcVideoStream();
           
            this.tabControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer_help)).BeginInit();
            this.splitContainer_help.Panel1.SuspendLayout();
            this.splitContainer_help.SuspendLayout();
            this.SuspendLayout();
            // 
            // add_camera_button
            // 
            this.add_camera_button.BackColor = System.Drawing.Color.White;
            this.add_camera_button.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("add_camera_button.BackgroundImage")));
            this.add_camera_button.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.add_camera_button.Cursor = System.Windows.Forms.Cursors.Hand;
            this.add_camera_button.Location = new System.Drawing.Point(0, 0);
            this.add_camera_button.Name = "add_camera_button";
            this.add_camera_button.Size = new System.Drawing.Size(40, 40);
            this.add_camera_button.TabIndex = 9;
            this.add_camera_button.UseVisualStyleBackColor = false;
            this.add_camera_button.Click += new System.EventHandler(this.button1_Click);
            // 
          
            // 
            // tabControl1
            // 
           
            this.tabControl1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.tabControl1.Location = new System.Drawing.Point(39, 3);
            this.tabControl1.Multiline = true;
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(425, 384);
            this.tabControl1.SizeMode = System.Windows.Forms.TabSizeMode.FillToRight;
            this.tabControl1.TabIndex = 8;
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
            // splitContainer_help
            // 
            this.splitContainer_help.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.splitContainer_help.Dock = System.Windows.Forms.DockStyle.Right;
            this.splitContainer_help.Location = new System.Drawing.Point(571, 0);
            this.splitContainer_help.Name = "splitContainer_help";
            this.splitContainer_help.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer_help.Panel1
            // 
            this.splitContainer_help.Panel1.Controls.Add(this.red_close_button);
            this.splitContainer_help.Panel1.Controls.Add(this.add_camera_button);
            this.splitContainer_help.Panel1.Controls.Add(this.tabControl1);
            this.splitContainer_help.Size = new System.Drawing.Size(467, 709);
            this.splitContainer_help.SplitterDistance = 390;
            this.splitContainer_help.TabIndex = 15;
            //
            // red_close_button
            // 
            this.red_close_button.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("red_close_button.BackgroundImage")));
            this.red_close_button.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.red_close_button.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.red_close_button.Location = new System.Drawing.Point(7, 46);
            this.red_close_button.Name = "red_close_button";
            this.red_close_button.Size = new System.Drawing.Size(26, 26);
            this.red_close_button.TabIndex = 1;
            this.red_close_button.UseVisualStyleBackColor = true;
            this.red_close_button.Click += new System.EventHandler(this.red_close_button_Click);
            // 
            // ucVideoStream1
            // 
            this.ucVideoStream1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ucVideoStream1.Location = new System.Drawing.Point(3, 3);
            this.ucVideoStream1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.ucVideoStream1.Name = "ucVideoStream1";
            this.ucVideoStream1.Size = new System.Drawing.Size(411, 352);
            this.ucVideoStream1.TabIndex = 0;
            // 
            // Help
            // 
            this.Controls.Add(this.splitContainer_help);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Name = "Help";
            this.Size = new System.Drawing.Size(1038, 709);
          
            this.tabControl1.ResumeLayout(false);
            this.splitContainer_help.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer_help)).EndInit();
            this.splitContainer_help.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button add_camera_button;
        private UcVideoStream ucVideoStream1;
        private BrightIdeasSoftware.HyperlinkStyle hyperlinkStyle1;
        private System.Windows.Forms.SplitContainer splitContainer_help;
        public System.Windows.Forms.Button red_close_button;
        public System.Windows.Forms.TabControl tabControl1;
    }
}
