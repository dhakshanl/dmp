using MissionPlanner.Controls;
using MissionPlanner.Controls.Dashboard;
using MissionPlanner.Properties;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews
{
    /// <summary>
    /// SIH26177 (Team Asunama) mission page: a 3D orbit view of the search area (explored
    /// floor, mapped walls, the drone and its trail, survivors, multi-class hazards with
    /// their onboard-assigned priority, and the highlighted rescue route to a selected
    /// survivor) as the centrepiece, with dual RGB/thermal camera tiles and the
    /// survivor/hazard list on the left, a bottom instrument strip (nav source with
    /// GPS/GPS-denied auto-switching, mode, mission clock, survivors found, area explored,
    /// arming, battery), and a telemetry/log/event drawer that opens only on request so
    /// the default view stays about the map and not a wall of numbers.
    ///
    /// Two data sources feed the same MissionScene/MissionStatus models: the real vehicle
    /// (MainV2.comPort.MAV.cs, wired but necessarily thin until the companion computer's
    /// on-device detection pipeline exists - see project memory) and MissionDemoSimulator,
    /// an explicit, operator-toggled stand-in - including a scripted low-battery
    /// autonomous dock/recharge/resume cycle - used to show the page with nothing plugged
    /// in. Demo mode never engages itself and never blends with live data.
    /// </summary>
    public partial class Mission : MyUserControl, IActivate, IDeactivate
    {
        private readonly System.Windows.Forms.Timer telemetryTimer;

        private readonly MissionScene scene = new MissionScene();
        private RescuePathPlanner planner;
        private int plannedForMapVersion = -1;

        private bool demoMode;
        private MissionDemoSimulator demoSim;

        // Which of sceneView/worldMapView is currently shown in the centre pane - the
        // page's "Automatic Switching" between GPS-denied (local 3D) and GPS-available
        // (world map) navigation. See SetGpsAvailable.
        private bool showingWorldMap;

        // ULog replay state.
        private bool ulogPlaybackActive;
        private readonly System.Windows.Forms.Timer ulogTimer;
        private List<ULog.message_data_s> ulogAttitude = new List<ULog.message_data_s>();
        private List<ULog.message_data_s> ulogBattery = new List<ULog.message_data_s>();
        private List<ULog.message_data_s> ulogLocalPos = new List<ULog.message_data_s>();
        private int idxAttitude, idxBattery, idxLocalPos;
        private ulong ulogBaseTimestamp;
        private ulong ulogEndTimestamp;
        private DateTime ulogWallStart;

        public Mission()
        {
            InitializeComponent();

            demoSim = new MissionDemoSimulator(scene);
            sceneView.Bind(scene);
            worldMapView.Bind(scene);
            survivorList.Bind(scene);

            telemetryTimer = new System.Windows.Forms.Timer { Interval = 100 };
            telemetryTimer.Tick += TelemetryTimer_Tick;

            ulogTimer = new System.Windows.Forms.Timer { Interval = 100 };
            ulogTimer.Tick += UlogTimer_Tick;
        }

        private void Mission_Load(object sender, EventArgs e)
        {
        }

        public void Activate()
        {
            telemetryTimer.Start();
            sceneView.Animate = true;
            UpdateConnectButton();

            if (demoMode)
                return;

            connectionHealthPanel.Attach(MainV2.comPort);
            RefreshLogBrowser();
            StartLiveFeed();
        }

        public void Deactivate()
        {
            telemetryTimer.Stop();
            ulogTimer.Stop();
            ulogPlaybackActive = false;
            connectionHealthPanel.Detach();
            sceneView.Animate = false;

            // Decoding video costs real CPU, so the pipelines only run while this tab is visible.
            liveFeedRgb.Stop();
            liveFeedThermal.Stop();
        }

        private const string MissionRgbPipelineKey = "mission_rgb_pipeline";
        private const string MissionThermalPipelineKey = "mission_thermal_pipeline";

        // Assumes the companion computer (per the SIH26177 hardware stack: RGB + 2K
        // thermal camera into an on-board Raspberry Pi 5) encodes each view to H.264 and
        // sends it to the GCS on its own UDP port - see companion\rgb_thermal_stream.py,
        // which sends on exactly these two ports by default.
        private const string DefaultRgbPipeline =
            "udpsrc port=5600 caps=\"application/x-rtp,media=video,encoding-name=H264,payload=96\" " +
            "! rtpjitterbuffer ! rtph264depay ! avdec_h264 " +
            "! videoconvert ! video/x-raw,format=BGRA ! appsink name=outsink sync=false";

        private const string DefaultThermalPipeline =
            "udpsrc port=5601 caps=\"application/x-rtp,media=video,encoding-name=H264,payload=96\" " +
            "! rtpjitterbuffer ! rtph264depay ! avdec_h264 " +
            "! videoconvert ! video/x-raw,format=BGRA ! appsink name=outsink sync=false";

        private void StartLiveFeed()
        {
            liveFeedRgb.Caption = "RGB";
            liveFeedRgb.Start(Settings.Instance[MissionRgbPipelineKey, DefaultRgbPipeline]);

            liveFeedThermal.Caption = "THERMAL";
            liveFeedThermal.Start(Settings.Instance[MissionThermalPipelineKey, DefaultThermalPipeline]);
        }

        private void RefreshLogBrowser()
        {
            try
            {
                logBrowser.RefreshLogs(Settings.Instance.LogDir);
            }
            catch
            {
                // best-effort — leave the tree empty if the configured log dir is unavailable
            }
        }

        #region header actions

        private void connectButton_Click(object sender, EventArgs e)
        {
            if (demoMode)
                return;

            // Reuses the main toolbar's own Connect button rather than re-implementing its
            // port/baud picker and connect-vs-disconnect logic here.
            try
            {
                MainV2.instance.MenuConnect.PerformClick();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Connect failed: " + ex.Message, Strings.ERROR);
            }

            UpdateConnectButton();
            connectionHealthPanel.Attach(MainV2.comPort);
        }

        private void UpdateConnectButton()
        {
            if (demoMode)
            {
                connectButton.Text = "Demo";
                connectButton.DotColor = MissionPalette.Warning;
                connectButton.Enabled = false;
                return;
            }

            connectButton.Enabled = true;
            var connected = IsConnected();
            connectButton.Text = connected ? "Disconnect" : "Connect";
            connectButton.DotColor = connected ? MissionPalette.Ok : MissionPalette.TextFaint;
        }

        private void drawerToggleButton_Click(object sender, EventArgs e)
        {
            drawer.Visible = !drawer.Visible;
            drawerToggleButton.Toggled = drawer.Visible;
            rootLayout.ColumnStyles[2] = new ColumnStyle(SizeType.Absolute, drawer.Visible ? DrawerWidth : 0f);
            rootLayout.PerformLayout();
        }

        /// <summary>
        /// Swaps the centre pane between the local 3D view and the world map, and keeps
        /// the map's markers current while it's the one showing. Idempotent - safe to call
        /// every tick with whatever MissionScene.GpsAvailable currently is.
        /// </summary>
        private void SetGpsAvailable(bool available)
        {
            if (available != showingWorldMap)
            {
                showingWorldMap = available;
                sceneView.Visible = !available;
                worldMapView.Visible = available;
                if (available)
                    worldMapView.FocusOnScene();
            }

            if (available)
                worldMapView.RefreshMarkers();
        }

        private void Scene_SurvivorSelected(int id)
        {
            scene.SelectedSurvivorId = id;
            EnsureRoute(id);
            sceneView.Invalidate();
            survivorList.Invalidate();
        }

        private void EnsureRoute(int id)
        {
            var s = scene.Survivors.Find(x => x.Id == id);
            if (s == null || s.Route != null)
                return;

            RebuildPlannerIfNeeded();
            s.Route = planner?.Plan(scene.Entry, s.Position);
        }

        private void RebuildPlannerIfNeeded()
        {
            var version = demoMode ? demoSim.MapVersion : plannedForMapVersion;
            if (planner != null && version == plannedForMapVersion)
                return;

            planner = RescuePathPlanner.Build(scene);
            plannedForMapVersion = version;

            // The map changed under a route already shown - a new hazard, say - so recompute it.
            foreach (var s in scene.Survivors)
                s.Route = planner.Plan(scene.Entry, s.Position);
        }

        #endregion

        #region demo mode

        private void demoToggleButton_Click(object sender, EventArgs e)
        {
            if (demoMode)
                ExitDemoMode();
            else
                EnterDemoMode();
        }

        /// <summary>
        /// Demo-only: manually force the other navigation mode, at any point in the demo -
        /// static preview or mid-flight. There's no real drone/GPS fix here to switch it
        /// automatically, so this is the stand-in; it stays in force until the flight is
        /// reset (Start Mission) or toggled again - see MissionDemoSimulator.SetGpsMode.
        /// </summary>
        private void gpsPreviewToggleButton_Click(object sender, EventArgs e)
        {
            if (!demoMode)
                return;

            demoSim.SetGpsMode(!scene.GpsAvailable);
            SetGpsAvailable(scene.GpsAvailable);
            UpdateGpsPreviewButton();
        }

        private void UpdateGpsPreviewButton()
        {
            gpsPreviewToggleButton.Toggled = scene.GpsAvailable;
            gpsPreviewToggleButton.Text = scene.GpsAvailable ? "Indoor View" : "World Map";
        }

        private void EnterDemoMode()
        {
            demoMode = true;

            ulogPlaybackActive = false;
            ulogTimer.Stop();
            connectionHealthPanel.Detach();

            // Preview shows a finished-looking map/telemetry immediately - nothing to wait
            // on for a quick look or a screenshot. Start Mission throws this away and flies
            // the real scripted discovery from a blank map instead (see StartMission).
            demoSim.Preview();
            plannedForMapVersion = -1;
            eventLog.Clear();
            telemetryReadout.ClearValues();
            SetGpsAvailable(scene.GpsAvailable);

            // Same clip on both tiles for now - a placeholder until distinct RGB/thermal
            // demo footage exists; captions still label them separately.
            var demoClip = VideoFeedPanel.FilePipeline(
                Settings.Instance["mission_demo_video", @"C:\Users\lnara\Downloads\zed2 slam.mp4"]);
            liveFeedRgb.Caption = "RGB - DEMO";
            liveFeedRgb.Start(demoClip);
            liveFeedRgb.Loop = true;
            liveFeedThermal.Caption = "THERMAL - DEMO";
            liveFeedThermal.Start(demoClip);
            liveFeedThermal.Loop = true;

            logBrowser.ShowDemoLogs();
            ulogStatusLabel.Text = "";
            logFileInfoLabel.Text = "file info: -";
            logFilePathLabel.Text = "File path: -";

            demoToggleButton.Toggled = true;
            gpsPreviewToggleButton.Visible = true;
            UpdateGpsPreviewButton();
            header.PerformLayout();
            header.SetPhase("DEMO — preview (press Start Mission to fly it live)", MissionPalette.TextDim);
            UpdateConnectButton();

            sceneView.Subtitle = "Demo scenario";
            sceneView.EmptyMessage = "Press Start Mission to fly the demo";
        }

        private void ExitDemoMode()
        {
            demoMode = false;
            liveFeedRgb.Loop = false;
            liveFeedThermal.Loop = false;

            scene.Clear();
            plannedForMapVersion = -1;
            sceneView.Subtitle = "Live map";
            sceneView.EmptyMessage = "Waiting for vehicle position";
            SetGpsAvailable(false);

            demoToggleButton.Toggled = false;
            gpsPreviewToggleButton.Visible = false;
            header.PerformLayout();
            header.SetPhase("", MissionPalette.TextDim);

            ResetTelemetryDisplay();
            UpdateConnectButton();

            connectionHealthPanel.Attach(MainV2.comPort);
            RefreshLogBrowser();
            StartLiveFeed();
        }

        private void ApplyDemoTick()
        {
            var s = demoSim.Sample();

            telemetryReadout.Set("alt", s.AltitudeM.ToString("0.00") + " m");
            telemetryReadout.Set("climb", s.ClimbRate.ToString("0.00") + " m/s");
            telemetryReadout.Set("speed", s.GroundSpeed.ToString("0.00") + " m/s");
            telemetryReadout.Set("roll", s.RollDeg.ToString("0.0") + "\u00b0");
            telemetryReadout.Set("pitch", s.PitchDeg.ToString("0.0") + "\u00b0");
            telemetryReadout.Set("yaw", s.YawDeg.ToString("0") + "\u00b0");
            telemetryReadout.Set("volts", s.BatteryVolts.ToString("0.00") + " V");
            telemetryReadout.Set("current", s.CurrentAmps.ToString("0.0") + " A");
            telemetryReadout.Set("batttemp", s.BatteryTempC.ToString("0.0") + " \u00b0C");
            telemetryReadout.Set("landed", s.Phase == DemoPhase.Charging ? "Charging" : demoSim.IsFlying ? "Flying" : "Landed");
            telemetryReadout.Set("timeinair", s.MissionTime.HasValue ? MissionPalette.FormatClock(s.MissionTime.Value) : "--");
            telemetryReadout.Set("timesincearm", s.MissionTime.HasValue ? MissionPalette.FormatClock(s.MissionTime.Value) : "--");

            connectionHealthPanel.ShowDemoSample(s.LinkQuality, s.BytesPerSecond, s.LatencyMs);

            statusBar.SetStatus(new MissionStatus
            {
                GpsFix = scene.GpsAvailable ? 3 : 0,
                Satellites = scene.GpsAvailable ? 12 : 0,
                NavSource = "LiDAR SLAM",
                Mode = s.Mode,
                Armed = s.Armed,
                BatteryPercent = s.BatteryPercent,
                BatteryVolts = s.BatteryVolts,
                MissionTime = s.MissionTime,
                SurvivorsFound = scene.Survivors.Count,
                ExploredFraction = scene.Explored.Fraction
            });

            header.SetPhase("DEMO — " + DescribePhase(s.Phase), PhaseColor(s.Phase));

            foreach (var ev in demoSim.DrainEvents())
                eventLog.Add(ev);

            RebuildPlannerIfNeeded();
            var selected = scene.SelectedSurvivor;
            if (selected != null && selected.Route == null)
                selected.Route = planner?.Plan(scene.Entry, selected.Position);

            SetGpsAvailable(scene.GpsAvailable);
            UpdateGpsPreviewButton();
            sceneView.Invalidate();
            survivorList.Invalidate();
        }

        private static string DescribePhase(DemoPhase phase)
        {
            switch (phase)
            {
                case DemoPhase.Arming: return "arming";
                case DemoPhase.Takeoff: return "taking off";
                case DemoPhase.Search: return "searching the area";
                case DemoPhase.Exit: return "heading for the exit";
                case DemoPhase.Landing: return "landing";
                case DemoPhase.Returning: return "aborted — returning to pad";
                case DemoPhase.DockingReturn: return "low battery — autonomous return to dock";
                case DemoPhase.Charging: return "charging at dock — mission will resume";
                case DemoPhase.Complete: return "mission complete";
                default: return "on the ground, ready";
            }
        }

        private static Color PhaseColor(DemoPhase phase)
        {
            switch (phase)
            {
                case DemoPhase.Returning:
                case DemoPhase.DockingReturn: return MissionPalette.Danger;
                case DemoPhase.Charging: return MissionPalette.Accent;
                case DemoPhase.Complete: return MissionPalette.Ok;
                case DemoPhase.Idle: return MissionPalette.TextDim;
                default: return MissionPalette.Warning;
            }
        }

        /// <summary>Puts every live-driven readout back to its no-data state on leaving demo mode.</summary>
        private void ResetTelemetryDisplay()
        {
            telemetryReadout.ClearValues();
            logProgressRing.Value = 0;
            ulogStatusLabel.Text = "";
            logFileInfoLabel.Text = "file info: ...";
            logFilePathLabel.Text = "File path: ...";
            statusBar.SetStatus(new MissionStatus());
        }

        #endregion

        private static bool IsConnected()
        {
            return MainV2.comPort?.MAV?.cs != null &&
                   (MainV2.comPort.BaseStream.IsOpen || MainV2.comPort.logreadmode);
        }

        private void TelemetryTimer_Tick(object sender, EventArgs e)
        {
            if (demoMode)
            {
                ApplyDemoTick();
                return;
            }

            UpdateConnectButton();

            if (ulogPlaybackActive)
                return;

            if (!IsConnected())
            {
                statusBar.SetStatus(new MissionStatus());
                scene.GpsAvailable = false;
                SetGpsAvailable(false);
                return;
            }

            var cs = MainV2.comPort.MAV.cs;

            telemetryReadout.Set("alt", cs.alt.ToString("0.00") + " m");
            telemetryReadout.Set("climb", cs.verticalspeed.ToString("0.00") + " m/s");
            telemetryReadout.Set("speed", cs.groundspeed.ToString("0.00") + " m/s");
            telemetryReadout.Set("roll", cs.roll.ToString("0.0") + "\u00b0");
            telemetryReadout.Set("pitch", cs.pitch.ToString("0.0") + "\u00b0");
            telemetryReadout.Set("yaw", cs.yaw.ToString("0") + "\u00b0");
            telemetryReadout.Set("volts", cs.battery_voltage.ToString("0.00") + " V");
            telemetryReadout.Set("current", cs.current.ToString("0.0") + " A");
            telemetryReadout.Set("batttemp", cs.battery_temp.ToString("0.0") + " \u00b0C");
            telemetryReadout.Set("landed", cs.landed ? "Landed" : "Flying");
            telemetryReadout.Set("timeinair", TimeSpan.FromSeconds(cs.timeInAir).ToString(@"mm\:ss"));
            telemetryReadout.Set("timesincearm", TimeSpan.FromSeconds(cs.timeSinceArmInAir).ToString(@"mm\:ss"));

            // Local NED position, when the flight controller is producing one - a
            // GPS-denied-capable estimator (visual/inertial) publishes into the same
            // LOCAL_POSITION_NED fields a GPS-based one would. Arena X = east, Y = north.
            scene.HasDrone = true;
            scene.Drone = new Point3(cs.pose, cs.posn, Math.Max(0, -cs.posd));
            scene.DroneYawDeg = cs.yaw;
            scene.DroneArmed = cs.armed;

            // "Automatic Switching": once there's a real fix, anchor the local frame onto
            // the vehicle's actual home position so FormatTag/the world map can geo-tag
            // with real lat/lon instead of the local grid - see MissionScene.GpsOrigin.
            scene.GpsAvailable = cs.gpsstatus >= 3;
            if (scene.GpsAvailable && (cs.HomeLocation.Lat != 0 || cs.HomeLocation.Lng != 0))
                scene.GpsOrigin = new GeoOrigin(cs.HomeLocation.Lat, cs.HomeLocation.Lng);

            statusBar.SetStatus(new MissionStatus
            {
                GpsFix = (int)cs.gpsstatus,
                Satellites = (int)cs.satcount,
                Mode = cs.mode,
                Armed = cs.armed,
                BatteryPercent = cs.battery_remaining,
                BatteryVolts = cs.battery_voltage,
                MissionTime = cs.armed ? TimeSpan.FromSeconds(cs.timeSinceArmInAir) : (TimeSpan?)null,
                SurvivorsFound = scene.Survivors.Count,
                ExploredFraction = scene.Explored.Fraction
            });

            SetGpsAvailable(scene.GpsAvailable);
            sceneView.Invalidate();
        }

        private void startMissionButton_Click(object sender, EventArgs e)
        {
            if (demoMode)
            {
                demoSim.StartMission();
                return;
            }

            if (!IsConnected())
            {
                CustomMessageBox.Show("Not connected.", Strings.ERROR);
                return;
            }

            try
            {
                MainV2.comPort.doARM(true);
                MainV2.comPort.setMode("Auto");
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Failed to start mission: " + ex.Message, Strings.ERROR);
            }
        }

        private void abortButton_Click(object sender, EventArgs e)
        {
            if (demoMode)
            {
                demoSim.Abort();
                return;
            }

            if (!IsConnected())
            {
                CustomMessageBox.Show("Not connected.", Strings.ERROR);
                return;
            }

            try
            {
                // Deliberately RTL, not a raw disarm — matches the rulebook's "emergency
                // recall" failsafe language rather than dropping the drone mid-air.
                MainV2.comPort.setMode("RTL");
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Failed to trigger RTL: " + ex.Message, Strings.ERROR);
            }
        }

        private void loadULogButton_Click(object sender, EventArgs e)
        {
            if (demoMode)
            {
                // No file picker in demo mode: a real log would drive the same readouts the
                // simulator is already writing to.
                return;
            }

            using (var ofd = new OpenFileDialog { Filter = "PX4 ULog (*.ulg)|*.ulg" })
            {
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                LogBrowser_FileSelected(ofd.FileName);
            }
        }

        private void LogBrowser_FileSelected(string path)
        {
            if (demoMode || path.StartsWith(MissionLogBrowser.DemoTagPrefix, StringComparison.Ordinal))
                return;

            logFileInfoLabel.Text = "file info: " + System.IO.Path.GetFileName(path);
            logFilePathLabel.Text = "File path: " + path;

            try
            {
                var topics = ULogLoader.Load(path);

                ulogAttitude = topics.Attitude;
                ulogBattery = topics.Battery;
                ulogLocalPos = topics.LocalPos;
                idxAttitude = idxBattery = idxLocalPos = 0;
                ulogBaseTimestamp = topics.BaseTimestamp;
                ulogEndTimestamp = topics.EndTimestamp;

                ulogPlaybackActive = true;
                ulogWallStart = DateTime.UtcNow;
                ulogStatusLabel.Text = "Playing: " + System.IO.Path.GetFileName(path);
                logProgressRing.Value = 0;
                ulogTimer.Stop();
                ulogTimer.Start();
            }
            catch (InvalidOperationException ex)
            {
                ulogStatusLabel.Text = ex.Message;
            }
            catch (Exception ex)
            {
                ulogStatusLabel.Text = "Failed to load log.";
                CustomMessageBox.Show("Failed to load ULog file: " + ex.Message, Strings.ERROR);
            }
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

            AdvanceAndApply(ulogAttitude, ref idxAttitude, targetTimestamp, ApplyReplayAttitude);
            AdvanceAndApply(ulogBattery, ref idxBattery, targetTimestamp, ApplyReplayBattery);
            AdvanceAndApply(ulogLocalPos, ref idxLocalPos, targetTimestamp, ApplyReplayLocalPosition);

            var pct = ulogEndTimestamp > ulogBaseTimestamp
                ? (targetTimestamp - ulogBaseTimestamp) * 100.0 / (ulogEndTimestamp - ulogBaseTimestamp)
                : 100.0;
            logProgressRing.Value = (int)Math.Max(0, Math.Min(100, pct));

            if (targetTimestamp >= ulogEndTimestamp)
            {
                ulogPlaybackActive = false;
                ulogTimer.Stop();
                ulogStatusLabel.Text = "Playback finished.";
            }
        }

        private void AdvanceAndApply(List<ULog.message_data_s> list, ref int idx, ulong targetTimestamp,
            Action<ULog.message_data_s> apply)
        {
            while (idx < list.Count && ULogLoader.GetTimestamp(list[idx]) <= targetTimestamp)
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

        private void ApplyReplayAttitude(ULog.message_data_s msg)
        {
            if (!ULogLoader.TryGetArray(msg.Raw, "q", out var q) || q.Length < 4)
                return;

            double qw = Convert.ToDouble(q[0]);
            double qx = Convert.ToDouble(q[1]);
            double qy = Convert.ToDouble(q[2]);
            double qz = Convert.ToDouble(q[3]);

            double rollRad = Math.Atan2(2 * (qw * qx + qy * qz), 1 - 2 * (qx * qx + qy * qy));
            double pitchRad = Math.Asin(Math.Max(-1, Math.Min(1, 2 * (qw * qy - qz * qx))));

            telemetryReadout.Set("roll", (rollRad * 180.0 / Math.PI).ToString("0.0") + "\u00b0");
            telemetryReadout.Set("pitch", (pitchRad * 180.0 / Math.PI).ToString("0.0") + "\u00b0");
        }

        private void ApplyReplayBattery(ULog.message_data_s msg)
        {
            if (ULogLoader.TryGetDouble(msg.Raw, "remaining", out var remaining))
                statusBar.SetStatus(new MissionStatus { BatteryPercent = (int)(remaining * 100) });

            if (ULogLoader.TryGetDouble(msg.Raw, "temperature", out var temp))
                telemetryReadout.Set("batttemp", temp.ToString("0.0") + " \u00b0C");
        }

        private void ApplyReplayLocalPosition(ULog.message_data_s msg)
        {
            if (!ULogLoader.TryGetDouble(msg.Raw, "z", out var z))
                return;

            telemetryReadout.Set("alt", (-z).ToString("0.00") + " m");
        }
    }
}
