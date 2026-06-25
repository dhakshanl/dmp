using MissionPlanner.Controls;
using MissionPlanner.Properties;
using MissionPlanner.Utilities;
using Org.BouncyCastle.Security.Certificates;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews
{
    public partial class Help : MyUserControl, IActivate
    {
        System.Windows.Forms.Timer telemetryTimer;

        // ULog replay state
        private bool ulogPlaybackActive = false;
        private System.Windows.Forms.Timer ulogTimer;
        private List<ULog.message_data_s> ulogAttitude = new List<ULog.message_data_s>();
        private List<ULog.message_data_s> ulogBattery = new List<ULog.message_data_s>();
        private List<ULog.message_data_s> ulogLocalPos = new List<ULog.message_data_s>();
        private List<ULog.message_data_s> ulogLandDetected = new List<ULog.message_data_s>();
        private List<ULog.message_data_s> ulogArmed = new List<ULog.message_data_s>();
        private int idxAttitude, idxBattery, idxLocalPos, idxLand, idxArmed;
        private ulong ulogBaseTimestamp;
        private ulong ulogEndTimestamp;
        private ulong? ulogArmedTimestamp;
        private ulong? ulogAirborneTimestamp;
        private DateTime ulogWallStart;

        public Help()
        {
            InitializeComponent();
            //  timer
            telemetryTimer = new System.Windows.Forms.Timer();
            telemetryTimer.Interval = 500;
            telemetryTimer.Tick += TelemetryTimer_Tick;
            telemetryTimer.Start();

            ulogTimer = new System.Windows.Forms.Timer();
            ulogTimer.Interval = 100;
            ulogTimer.Tick += UlogTimer_Tick;
        }

        private void Help_Load(object sender, EventArgs e)
        {
        }

        public void Activate()
        {

        }

        private void buttonLoadULog_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog { Filter = "PX4 ULog (*.ulg)|*.ulg" })
            {
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    var ulog = new ULog();
                    using (var fs = File.OpenRead(ofd.FileName))
                        ulog.read(fs);

                    ulogAttitude = ulog.Data.Where(a => a.Type == "vehicle_attitude" && a.MultiID == 0).OrderBy(GetTimestamp).ToList();
                    ulogBattery = ulog.Data.Where(a => a.Type == "battery_status" && a.MultiID == 0).OrderBy(GetTimestamp).ToList();
                    ulogLocalPos = ulog.Data.Where(a => a.Type == "vehicle_local_position" && a.MultiID == 0).OrderBy(GetTimestamp).ToList();
                    ulogLandDetected = ulog.Data.Where(a => a.Type == "vehicle_land_detected" && a.MultiID == 0).OrderBy(GetTimestamp).ToList();
                    ulogArmed = ulog.Data.Where(a => a.Type == "actuator_armed" && a.MultiID == 0).OrderBy(GetTimestamp).ToList();

                    var allTimestamps = ulogAttitude.Concat(ulogBattery).Concat(ulogLocalPos).Concat(ulogLandDetected).Concat(ulogArmed)
                        .Select(GetTimestamp).Where(t => t > 0).ToList();

                    if (allTimestamps.Count == 0)
                    {
                        guna2HtmlLabelULogStatus.Text = "No recognised topics found in this log.";
                        return;
                    }

                    idxAttitude = idxBattery = idxLocalPos = idxLand = idxArmed = 0;
                    ulogArmedTimestamp = null;
                    ulogAirborneTimestamp = null;
                    ulogBaseTimestamp = allTimestamps.Min();
                    ulogEndTimestamp = allTimestamps.Max();

                    ulogPlaybackActive = true;
                    ulogWallStart = DateTime.UtcNow;
                    guna2HtmlLabelULogStatus.Text = $"Playing: {Path.GetFileName(ofd.FileName)}";
                    ulogTimer.Stop();
                    ulogTimer.Start();
                }
                catch (Exception ex)
                {
                    guna2HtmlLabelULogStatus.Text = "Failed to load log.";
                    CustomMessageBox.Show("Failed to load ULog file: " + ex.Message, Strings.ERROR);
                }
            }
        }

        private static ulong GetTimestamp(ULog.message_data_s msg)
        {
            if (msg.Raw != null && msg.Raw.TryGetValue("timestamp", out var t))
                return Convert.ToUInt64(t);
            return 0;
        }

        private static bool TryGetDouble(Dictionary<string, object> raw, string key, out double value)
        {
            value = 0;
            if (raw == null || !raw.TryGetValue(key, out var obj) || obj == null)
                return false;
            try
            {
                value = Convert.ToDouble(obj);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetArray(Dictionary<string, object> raw, string key, out object[] value)
        {
            value = null;
            if (raw == null || !raw.TryGetValue(key, out var obj) || !(obj is object[] arr))
                return false;
            value = arr;
            return true;
        }

        private static int CellVoltsToPercent(double volts)
        {
            const double empty = 3.3;
            const double full = 4.2;
            return (int)Math.Max(0, Math.Min(100, (volts - empty) / (full - empty) * 100.0));
        }

        private void UlogTimer_Tick(object sender, EventArgs e)
        {
            if (!ulogPlaybackActive)
            {
                ulogTimer.Stop();
                return;
            }

            var elapsedUs = (ulong)Math.Max(0, (DateTime.UtcNow - ulogWallStart).TotalMilliseconds * 1000.0);
            var targetTimestamp = ulogBaseTimestamp + elapsedUs;

            AdvanceAndApply(ulogAttitude, ref idxAttitude, targetTimestamp, ApplyAttitude);
            AdvanceAndApply(ulogBattery, ref idxBattery, targetTimestamp, ApplyBattery);
            AdvanceAndApply(ulogLocalPos, ref idxLocalPos, targetTimestamp, ApplyLocalPosition);
            AdvanceAndApply(ulogLandDetected, ref idxLand, targetTimestamp, ApplyLanded);
            AdvanceAndApply(ulogArmed, ref idxArmed, targetTimestamp, ApplyArmed);

            if (ulogArmedTimestamp.HasValue)
            {
                var armedSec = (targetTimestamp - ulogArmedTimestamp.Value) / 1_000_000.0;
                guna2TextBoxTimeSinceArm.Text = TimeSpan.FromSeconds(Math.Max(0, armedSec)).ToString(@"hh\:mm\:ss");
            }
            if (ulogAirborneTimestamp.HasValue)
            {
                var airSec = (targetTimestamp - ulogAirborneTimestamp.Value) / 1_000_000.0;
                guna2TextBoxTimeInAir.Text = TimeSpan.FromSeconds(Math.Max(0, airSec)).ToString(@"hh\:mm\:ss");
            }

            var pct = ulogEndTimestamp > ulogBaseTimestamp
                ? (targetTimestamp - ulogBaseTimestamp) * 100.0 / (ulogEndTimestamp - ulogBaseTimestamp)
                : 100.0;
            guna2HtmlLabelULogStatus.Text = $"Playing: {Math.Min(100, pct):0}%";

            if (targetTimestamp >= ulogEndTimestamp)
            {
                ulogPlaybackActive = false;
                ulogTimer.Stop();
                guna2HtmlLabelULogStatus.Text = "Playback finished.";
            }
        }

        private void AdvanceAndApply(List<ULog.message_data_s> list, ref int idx, ulong targetTimestamp, Action<ULog.message_data_s> apply)
        {
            while (idx < list.Count && GetTimestamp(list[idx]) <= targetTimestamp)
            {
                try
                {
                    apply(list[idx]);
                }
                catch
                {
                    // skip malformed/unexpected message shape rather than aborting playback
                }
                idx++;
            }
        }

        private void ApplyAttitude(ULog.message_data_s msg)
        {
            if (!TryGetArray(msg.Raw, "q", out var q) || q.Length < 4)
                return;

            double qw = Convert.ToDouble(q[0]);
            double qx = Convert.ToDouble(q[1]);
            double qy = Convert.ToDouble(q[2]);
            double qz = Convert.ToDouble(q[3]);

            double roll = Math.Atan2(2 * (qw * qx + qy * qz), 1 - 2 * (qx * qx + qy * qy));
            double pitch = Math.Asin(Math.Max(-1, Math.Min(1, 2 * (qw * qy - qz * qx))));

            guna2TextBoxRoll.Text = (roll * 180.0 / Math.PI).ToString("0.00") + "°";
            guna2TextBoxPitch.Text = (pitch * 180.0 / Math.PI).ToString("0.00") + "°";
        }

        private void ApplyBattery(ULog.message_data_s msg)
        {
            if (TryGetDouble(msg.Raw, "remaining", out var remaining))
            {
                guna2CircleProgressBarCHARGE.Value = Math.Max(0, Math.Min(100, (int)(remaining * 100)));
            }

            if (TryGetDouble(msg.Raw, "temperature", out var temp))
            {
                guna2TextBoxBatteryTemperature.Text = temp.ToString("0.0") + " °C";
            }

            if (TryGetArray(msg.Raw, "voltage_cell_v", out var cells))
            {
                if (cells.Length > 0) UpdateCellBar(guna2VProgressBarCell1, CellVoltsToPercent(Convert.ToDouble(cells[0])));
                if (cells.Length > 1) UpdateCellBar(guna2VProgressBarCell2, CellVoltsToPercent(Convert.ToDouble(cells[1])));
                if (cells.Length > 2) UpdateCellBar(guna2VProgressBarCell3, CellVoltsToPercent(Convert.ToDouble(cells[2])));
            }
        }

        private void ApplyLocalPosition(ULog.message_data_s msg)
        {
            if (TryGetDouble(msg.Raw, "z", out var z))
            {
                guna2TextBoxAltitude.Text = (-z).ToString("0.00") + " m";
            }
        }

        private void ApplyLanded(ULog.message_data_s msg)
        {
            if (msg.Raw != null && msg.Raw.TryGetValue("landed", out var landedObj))
            {
                bool landed = Convert.ToBoolean(landedObj);
                guna2TextBoxLandedState.Text = landed ? "Landed" : "Flying";

                if (!landed && ulogAirborneTimestamp == null)
                    ulogAirborneTimestamp = GetTimestamp(msg);
                else if (landed)
                    ulogAirborneTimestamp = null;
            }
        }

        private void ApplyArmed(ULog.message_data_s msg)
        {
            if (msg.Raw != null && msg.Raw.TryGetValue("armed", out var armedObj))
            {
                bool armed = Convert.ToBoolean(armedObj);
                if (armed && ulogArmedTimestamp == null)
                    ulogArmedTimestamp = GetTimestamp(msg);
                else if (!armed)
                    ulogArmedTimestamp = null;
            }
        }

        private void TelemetryTimer_Tick(object sender, EventArgs e)
        {
            if (ulogPlaybackActive)
                return;

            if (MainV2.comPort?.MAV?.cs != null && (MainV2.comPort.BaseStream.IsOpen || MainV2.comPort.logreadmode))
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

        private void guna2HtmlLabel1_Click(object sender, EventArgs e)
        {

        }

        private void guna2ProgressBar1_ValueChanged(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabel2_Click(object sender, EventArgs e)
        {

        }
    }
}