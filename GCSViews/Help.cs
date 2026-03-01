using MissionPlanner.Controls;
using MissionPlanner.Properties;
using MissionPlanner.Utilities;
using Org.BouncyCastle.Security.Certificates;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews
{
    public partial class Help : MyUserControl, IActivate
    {
        System.Windows.Forms.Timer telemetryTimer;
        public Help()
        {
            InitializeComponent();
            //  timer
            telemetryTimer = new System.Windows.Forms.Timer();
            telemetryTimer.Interval = 500; 
            telemetryTimer.Tick += TelemetryTimer_Tick;
            telemetryTimer.Start();

        }

        public void Activate()
        {
           
        }
        private void TelemetryTimer_Tick(object sender, EventArgs e)
        {
            if (MainV2.comPort?.MAV?.cs != null && MainV2.comPort.BaseStream.IsOpen)
            {
                var cs = MainV2.comPort.MAV.cs;

                // =============================
                // Battery Remaining (Circle)
                // =============================
                double batt = cs.battery_remaining;
                guna2CircleProgressBarCHARGE.Value = Math.Max(0, Math.Min(100, (int)batt));

                // =============================
                // Altitude
                // =============================
                guna2TextBoxAltitude.Text = cs.alt.ToString("0.00") + " m";

                // =============================
                // Pitch & Roll
                // =============================
                guna2TextBoxPitch.Text = cs.pitch.ToString("0.00") + "°";
                guna2TextBoxRoll.Text = cs.roll.ToString("0.00") + "°";

                // =============================
                // Battery Temperature
                // =============================
                guna2TextBoxBatteryTemperature.Text = cs.battery_temp.ToString("0.0") + " °C";

                // =============================
                // Landed State
                // =============================
                guna2TextBoxLandedState.Text = cs.landed ? "Landed" : "Flying";

                // =============================
                // Time in Air
                // =============================
                TimeSpan airTime = TimeSpan.FromSeconds(cs.timeInAir);
                guna2TextBoxTimeInAir.Text = airTime.ToString(@"hh\:mm\:ss");

                // =============================
                // Time Since Arm
                // =============================
                TimeSpan armTime = TimeSpan.FromSeconds(cs.timeSinceArmInAir);
                guna2TextBoxTimeSinceArm.Text = armTime.ToString(@"hh\:mm\:ss");

                // =============================
                // Battery Cell Voltages
                // =============================
                UpdateCellBar(guna2VProgressBarCell1, cs.battery_remaining2);
                UpdateCellBar(guna2VProgressBarCell2, cs.battery_remaining3);
                UpdateCellBar(guna2VProgressBarCell3, cs.battery_remaining4);
                UpdateCellBar(guna2VProgressBarCell4, cs.battery_remaining5);
                UpdateCellBar(guna2VProgressBarCell5, cs.battery_remaining6);
                UpdateCellBar(guna2VProgressBarCell6, cs.battery_remaining7);
                UpdateCellBar(guna2VProgressBarCell7, cs.battery_remaining8);
                UpdateCellBar(guna2VProgressBarCell8, cs.battery_remaining9);

                // =============================
                // Data Transferred %
                // =============================
                
                       // YET TO FILL
            }
        }
        private void UpdateCellBar(Guna.UI2.WinForms.Guna2VProgressBar bar, int percent)
        {
            if (percent < 0)
            {
                bar.Value = 0;
                return;
            }

            bar.Value = Math.Max(0, Math.Min(100, percent));

            // Color Logic
            if (percent < 20)
                bar.ProgressColor = System.Drawing.Color.Red;
            else if (percent < 40)
                bar.ProgressColor = System.Drawing.Color.Orange;
            else
                bar.ProgressColor = System.Drawing.Color.Cyan;
        }

        public void BUT_updatecheck_Click(object sender, EventArgs e)
        {
            try
            {
                if (Program.WindowsStoreApp)
                {
                    return;
                }
                Utilities.Update.CheckForUpdate(true);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.ToString(), Strings.ERROR);
            }
        }

        private void Help_Load(object sender, EventArgs e)
        {
        }
       
        private void guna2CircleProgressBarCHARGE_ValueChanged(object sender, EventArgs e)
        {
            if (guna2CircleProgressBarCHARGE.Value < 20)
            {
                guna2CircleProgressBarCHARGE.ProgressColor2 = System.Drawing.Color.OrangeRed;
                guna2CircleProgressBarCHARGE.ProgressColor = System.Drawing.Color.Red;
            }
        }
        private void guna2HtmlLabelDataTransferred_Click(object sender, EventArgs e)
        {

        }

        private void guna2TextBox2_TextChanged(object sender, EventArgs e)
        {

        }

        

        private void guna2TextBoxPitch_TextChanged(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelAltitude_Click(object sender, EventArgs e)
        {

        }

        private void guna2TextBoxRoll_TextChanged(object sender, EventArgs e)
        {

        }

        private void guna2TextBoxTimeInAir_TextChanged(object sender, EventArgs e)
        {

        }

        private void guna2TextBoxLandedState_TextChanged(object sender, EventArgs e)
        {

        }

        private void guna2TextBoxBatteryTemperature_TextChanged(object sender, EventArgs e)
        {

        }

        private void guna2TextBoxTimeSinceArm_TextChanged(object sender, EventArgs e)
        {

        }

        private void guna2TextBoxPitch_TextChanged_1(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelBatteryTemperature_Click(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelTimeInAir_Click(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelLandedState_Click(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelTimeSinceArm_Click(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelRoll_Click(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelPitch_Click(object sender, EventArgs e)
        {

        }
        

        private void guna2HtmlLabelCell5_Click(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelCell4_Click(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelCell3_Click(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelCell6_Click(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelCell7_Click(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelCell8_Click(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelCell2_Click(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelCell1_Click(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabelBatteryRemaining_Click(object sender, EventArgs e)
        {

        }

        private void guna2VProgressBarCell1_ValueChanged(object sender, EventArgs e)
        {
            if (guna2VProgressBarCell1.Value < 20)
            {
                guna2VProgressBarCell1.ProgressColor = System.Drawing.Color.Red;
            }
            else if (guna2VProgressBarCell1.Value < 40)
            {
                guna2VProgressBarCell1.ProgressColor = System.Drawing.Color.Orange;
            }
            else
            {
                guna2VProgressBarCell1.ProgressColor = System.Drawing.Color.Cyan;
            }
        }

        private void guna2VProgressBarCell2_ValueChanged(object sender, EventArgs e)
        {
            if (guna2VProgressBarCell2.Value < 20)
            {
                guna2VProgressBarCell2.ProgressColor = System.Drawing.Color.Red;
            }
            else if (guna2VProgressBarCell2.Value < 40)
            {
                guna2VProgressBarCell2.ProgressColor = System.Drawing.Color.Orange;
            }
            else
            {
                guna2VProgressBarCell2.ProgressColor = System.Drawing.Color.Cyan;
            }
        }

        private void guna2VProgressBarCell3_ValueChanged(object sender, EventArgs e)
        {
            if (guna2VProgressBarCell3.Value < 20)
            {
                guna2VProgressBarCell3.ProgressColor = System.Drawing.Color.Red;
            }
            else if (guna2VProgressBarCell3.Value < 40)
            {
                guna2VProgressBarCell3.ProgressColor = System.Drawing.Color.Orange;
            }
            else
            {
                guna2VProgressBarCell3.ProgressColor = System.Drawing.Color.Cyan;
            }
        }

        private void guna2VProgressBarCell4_ValueChanged(object sender, EventArgs e)
        {
            if (guna2VProgressBarCell4.Value < 20)
            {
                guna2VProgressBarCell4.ProgressColor = System.Drawing.Color.Red;
            }
            else if (guna2VProgressBarCell4.Value < 40)
            {
                guna2VProgressBarCell4.ProgressColor = System.Drawing.Color.Orange;
            }
            else
            {
                guna2VProgressBarCell4.ProgressColor = System.Drawing.Color.Cyan;
            }
        }

        private void guna2VProgressBarCell5_ValueChanged(object sender, EventArgs e)
        {
            if (guna2VProgressBarCell5.Value < 20)
            {
                guna2VProgressBarCell5.ProgressColor = System.Drawing.Color.Red;
            }
            else if (guna2VProgressBarCell5.Value < 40)
            {
                guna2VProgressBarCell5.ProgressColor = System.Drawing.Color.Orange;
            }
            else
            {
                guna2VProgressBarCell5.ProgressColor = System.Drawing.Color.Cyan;
            }
        }

        private void guna2VProgressBarCell6_ValueChanged(object sender, EventArgs e)
        {
            if (guna2VProgressBarCell6.Value < 20)
            {
                guna2VProgressBarCell6.ProgressColor = System.Drawing.Color.Red;
            }
            else if (guna2VProgressBarCell6.Value < 40)
            {
                guna2VProgressBarCell6.ProgressColor = System.Drawing.Color.Orange;
            }
            else
            {
                guna2VProgressBarCell6.ProgressColor = System.Drawing.Color.Cyan;
            }
        }

        private void guna2VProgressBarCell7_ValueChanged(object sender, EventArgs e)
        {
            if (guna2VProgressBarCell7.Value < 20)
            {
                guna2VProgressBarCell7.ProgressColor = System.Drawing.Color.Red;
            }
            else if (guna2VProgressBarCell7.Value < 40)
            {
                guna2VProgressBarCell7.ProgressColor = System.Drawing.Color.Orange;
            }
            else
            {
                guna2VProgressBarCell7.ProgressColor = System.Drawing.Color.Cyan;
            }
        }

        private void guna2VProgressBarCell8_ValueChanged(object sender, EventArgs e)
        {
            if (guna2VProgressBarCell8.Value < 20)
            {
                guna2VProgressBarCell8.ProgressColor = System.Drawing.Color.Red;
            }
            else if (guna2VProgressBarCell8.Value < 40)
            {
                guna2VProgressBarCell8.ProgressColor = System.Drawing.Color.Orange;
            }
            else
            {   
                guna2VProgressBarCell8.ProgressColor = System.Drawing.Color.Cyan;
            }
        }
    }
}