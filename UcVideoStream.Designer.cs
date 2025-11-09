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
            this.VideoBox.Name = "VideoBox";
            this.VideoBox.Padding = new System.Windows.Forms.Padding(10);
            this.VideoBox.Size = new System.Drawing.Size(635, 426);
            this.VideoBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.VideoBox.TabIndex = 7;
            this.VideoBox.TabStop = false;
            this.VideoBox.WaitOnLoad = true;
            this.VideoBox.Click += new System.EventHandler(this.VideoBox_Click);
            // 
            // stop_camera
            // 
            this.stop_camera.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)));
            this.stop_camera.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.stop_camera.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.stop_camera.Location = new System.Drawing.Point(346, 450);
            this.stop_camera.Name = "stop_camera";
            this.stop_camera.Padding = new System.Windows.Forms.Padding(5);
            this.stop_camera.Size = new System.Drawing.Size(207, 96);
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
            this.start_camera.Location = new System.Drawing.Point(100, 450);
            this.start_camera.Name = "start_camera";
            this.start_camera.Padding = new System.Windows.Forms.Padding(5);
            this.start_camera.Size = new System.Drawing.Size(192, 96);
            this.start_camera.TabIndex = 8;
            this.start_camera.Text = "start camera";
            this.start_camera.TextColorNotEnabled = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(87)))), ((int)(((byte)(4)))));
            this.start_camera.UseVisualStyleBackColor = true;
            this.start_camera.Click += new System.EventHandler(this.start_camera_Click);
            // 
            // UcVideoStream
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.stop_camera);
            this.Controls.Add(this.start_camera);
            this.Controls.Add(this.VideoBox);
            this.Name = "UcVideoStream";
            this.Size = new System.Drawing.Size(635, 563);
            ((System.ComponentModel.ISupportInitialize)(this.VideoBox)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        public System.Windows.Forms.PictureBox VideoBox;
        private Controls.MyButton start_camera;
        private Controls.MyButton stop_camera;
    }
}
