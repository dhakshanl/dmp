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
            this.start_camera = new MissionPlanner.Controls.MyButton();
            this.stop_camera = new MissionPlanner.Controls.MyButton();
            this.VideoBox = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.VideoBox)).BeginInit();
            this.SuspendLayout();
            // 
            // start_camera
            // 
            resources.ApplyResources(this.start_camera, "start_camera");
            this.start_camera.Name = "start_camera";
            this.start_camera.TextColorNotEnabled = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(87)))), ((int)(((byte)(4)))));
            this.start_camera.UseVisualStyleBackColor = true;
            this.start_camera.Click += new System.EventHandler(this.start_camera_Click);
            // 
            // stop_camera
            // 
            resources.ApplyResources(this.stop_camera, "stop_camera");
            this.stop_camera.Name = "stop_camera";
            this.stop_camera.TextColorNotEnabled = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(87)))), ((int)(((byte)(4)))));
            this.stop_camera.UseVisualStyleBackColor = true;
            this.stop_camera.Click += new System.EventHandler(this.stop_camera_Click);
            // 
            // VideoBox
            // 
            resources.ApplyResources(this.VideoBox, "VideoBox");
            this.VideoBox.BackgroundImage = global::MissionPlanner.Properties.Resources.camera_icon;
            this.VideoBox.ErrorImage = global::MissionPlanner.Properties.Resources.no_video;
            this.VideoBox.Image = global::MissionPlanner.Properties.Resources.no_video;
            this.VideoBox.InitialImage = global::MissionPlanner.Properties.Resources.camera_icon;
            this.VideoBox.Name = "VideoBox";
            this.VideoBox.TabStop = false;
            this.VideoBox.Click += new System.EventHandler(this.VideoBox_Click);
            // 
            // Help
            // 
            this.Controls.Add(this.VideoBox);
            this.Controls.Add(this.stop_camera);
            this.Controls.Add(this.start_camera);
            resources.ApplyResources(this, "$this");
            this.Name = "Help";
            this.Load += new System.EventHandler(this.Help_Load);
            ((System.ComponentModel.ISupportInitialize)(this.VideoBox)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private Controls.MyButton start_camera;
        private Controls.MyButton stop_camera;
        public System.Windows.Forms.PictureBox VideoBox;
    }
}
