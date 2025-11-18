namespace MissionPlanner
{
    partial class UcVideoStream
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
            this.VideoBox = new System.Windows.Forms.PictureBox();
            this.stop_camera = new MissionPlanner.Controls.MyButton();
            this.start_camera = new MissionPlanner.Controls.MyButton();
            ((System.ComponentModel.ISupportInitialize)(this.VideoBox)).BeginInit();
            this.SuspendLayout();
            // 
            // VideoBox
            // 
            this.VideoBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.VideoBox.BackgroundImage = global::MissionPlanner.Properties.Resources.bgdark;
            this.VideoBox.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.VideoBox.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.VideoBox.ErrorImage = global::MissionPlanner.Properties.Resources.no_video;
            this.VideoBox.Image = global::MissionPlanner.Properties.Resources.no_video;
            this.VideoBox.InitialImage = global::MissionPlanner.Properties.Resources.camera_icon;
            this.VideoBox.Location = new System.Drawing.Point(0, 0);
            this.VideoBox.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.VideoBox.Name = "VideoBox";
            this.VideoBox.Padding = new System.Windows.Forms.Padding(7, 6, 7, 6);
            this.VideoBox.Size = new System.Drawing.Size(425, 278);
            this.VideoBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.VideoBox.TabIndex = 7;
            this.VideoBox.TabStop = false;
            this.VideoBox.WaitOnLoad = true;
           
            // 
            // stop_camera
            // 
            this.stop_camera.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)));
            this.stop_camera.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.stop_camera.Enabled = false;
            this.stop_camera.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.stop_camera.Location = new System.Drawing.Point(231, 292);
            this.stop_camera.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.stop_camera.Name = "stop_camera";
            this.stop_camera.Padding = new System.Windows.Forms.Padding(3, 3, 3, 3);
            this.stop_camera.Size = new System.Drawing.Size(138, 62);
            this.stop_camera.TabIndex = 9;
            this.stop_camera.Text = "stop camera";
            this.stop_camera.TextColorNotEnabled = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(87)))), ((int)(((byte)(4)))));
            this.stop_camera.UseVisualStyleBackColor = true;
            this.stop_camera.Click += new System.EventHandler(this.stop_camera_Click);
            // 
            // start_camera
            // 
            this.start_camera.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)));
            this.start_camera.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.start_camera.Location = new System.Drawing.Point(67, 292);
            this.start_camera.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.start_camera.Name = "start_camera";
            this.start_camera.Padding = new System.Windows.Forms.Padding(3, 3, 3, 3);
            this.start_camera.Size = new System.Drawing.Size(128, 62);
            this.start_camera.TabIndex = 8;
            this.start_camera.Text = "start camera";
            this.start_camera.TextColorNotEnabled = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(87)))), ((int)(((byte)(4)))));
            this.start_camera.UseVisualStyleBackColor = true;
            this.start_camera.Click += new System.EventHandler(this.start_camera_Click);
            // 
            // UcVideoStream
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.stop_camera);
            this.Controls.Add(this.start_camera);
            this.Controls.Add(this.VideoBox);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "UcVideoStream";
            this.Size = new System.Drawing.Size(423, 366);
            ((System.ComponentModel.ISupportInitialize)(this.VideoBox)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        public System.Windows.Forms.PictureBox VideoBox;
        private Controls.MyButton start_camera;
        private Controls.MyButton stop_camera;
    }
}
