namespace MissionPlanner
{
    partial class ChargingInfo
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
            this.components = new System.ComponentModel.Container();
            this.BatteryProgressBar = new MissionPlanner.Controls.MyProgressBar();
            this.BatteryPercentage = new System.Windows.Forms.RichTextBox();
            this.BatteryRemaining = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.timer2 = new System.Windows.Forms.Timer(this.components);
            this.configBatteryMonitoring1 = new MissionPlanner.GCSViews.ConfigurationView.ConfigBatteryMonitoring();
            this.SuspendLayout();
            // 
            // BatteryProgressBar
            // 
            this.BatteryProgressBar.AccessibleDescription = "";
            this.BatteryProgressBar.AccessibleName = "";
            this.BatteryProgressBar.BackColor = System.Drawing.Color.Transparent;
            this.BatteryProgressBar.BGGradBot = System.Drawing.Color.FromArgb(((int)(((byte)(127)))), ((int)(((byte)(167)))), ((int)(((byte)(42)))));
            this.BatteryProgressBar.BGGradTop = System.Drawing.Color.FromArgb(((int)(((byte)(102)))), ((int)(((byte)(139)))), ((int)(((byte)(26)))));
            this.BatteryProgressBar.ForeColor = System.Drawing.Color.DarkGray;
            this.BatteryProgressBar.Location = new System.Drawing.Point(20, 29);
            this.BatteryProgressBar.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.BatteryProgressBar.Name = "BatteryProgressBar";
            this.BatteryProgressBar.Outline = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(171)))), ((int)(((byte)(112)))));
            this.BatteryProgressBar.Size = new System.Drawing.Size(359, 19);
            this.BatteryProgressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.BatteryProgressBar.TabIndex = 8;
            this.BatteryProgressBar.Tag = "";
            this.BatteryProgressBar.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(54)))), ((int)(((byte)(8)))));
            this.BatteryProgressBar.Value = 56;
            this.BatteryProgressBar.Visible = false;
            this.BatteryProgressBar.Load += new System.EventHandler(this.BatteryProgressBar_Load);
            // 
            // BatteryPercentage
            // 
            this.BatteryPercentage.Location = new System.Drawing.Point(382, 29);
            this.BatteryPercentage.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.BatteryPercentage.Name = "BatteryPercentage";
            this.BatteryPercentage.Size = new System.Drawing.Size(43, 21);
            this.BatteryPercentage.TabIndex = 9;
            this.BatteryPercentage.Text = "0%";
            this.BatteryPercentage.TextChanged += new System.EventHandler(this.richTextBox1_TextChanged);
            // 
            // BatteryRemaining
            // 
            this.BatteryRemaining.AutoSize = true;
            this.BatteryRemaining.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.BatteryRemaining.Location = new System.Drawing.Point(16, 3);
            this.BatteryRemaining.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.BatteryRemaining.Name = "BatteryRemaining";
            this.BatteryRemaining.Size = new System.Drawing.Size(140, 17);
            this.BatteryRemaining.TabIndex = 10;
            this.BatteryRemaining.Text = "battery remaining:";
            // 
            // configBatteryMonitoring1
            // 
            this.configBatteryMonitoring1.Location = new System.Drawing.Point(30, 88);
            this.configBatteryMonitoring1.Name = "configBatteryMonitoring1";
            this.configBatteryMonitoring1.Size = new System.Drawing.Size(512, 322);
            this.configBatteryMonitoring1.TabIndex = 11;
            this.configBatteryMonitoring1.Load += new System.EventHandler(this.configBatteryMonitoring1_Load);
            // 
            // ChargingInfo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.configBatteryMonitoring1);
            this.Controls.Add(this.BatteryRemaining);
            this.Controls.Add(this.BatteryPercentage);
            this.Controls.Add(this.BatteryProgressBar);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "ChargingInfo";
            this.Size = new System.Drawing.Size(634, 448);
            this.Load += new System.EventHandler(this.ChargingInfo_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Controls.MyProgressBar BatteryProgressBar;
        private System.Windows.Forms.RichTextBox BatteryPercentage;
        private System.Windows.Forms.Label BatteryRemaining;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Timer timer2;
        private GCSViews.ConfigurationView.ConfigBatteryMonitoring configBatteryMonitoring1;
    }
}
