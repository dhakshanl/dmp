using System;
using System.Drawing;
using System.Windows.Forms;
using MissionPlanner.Controls.Dashboard;

namespace MissionPlanner.GCSViews
{
    public partial class Mission
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                components?.Dispose();
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private TableLayoutPanel rootLayout;

        private MissionHeader header;
        private MissionActionButton startMissionButton;
        private MissionActionButton abortButton;
        private MissionActionButton demoToggleButton;
        private MissionActionButton drawerToggleButton;
        private MissionActionButton connectButton;

        private MissionSurface leftRail;
        private VideoFeedPanel liveFeedRgb;
        private VideoFeedPanel liveFeedThermal;
        private SurvivorListView survivorList;
        private SectionHeader survivorsHeader;

        private MissionSceneView sceneView;
        private MissionWorldMapView worldMapView;

        private MissionSurface drawer;
        private TelemetryReadout telemetryReadout;
        private MissionActionButton loadULogButton;
        private MissionProgressRing logProgressRing;
        private MissionLogBrowser logBrowser;
        private Label logFileInfoLabel;
        private Label logFilePathLabel;
        private Label ulogStatusLabel;
        private ConnectionHealthPanel connectionHealthPanel;
        private MissionEventLog eventLog;

        private MissionStatusBar statusBar;

        private const int RailWidth = 300;
        private const int DrawerWidth = 340;

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.BackColor = MissionPalette.Background;
            this.Name = "Mission";
            this.Size = new Size(1360, 860);

            header = new MissionHeader { Dock = DockStyle.Top, Title = "Autonomous SAR Mission" };
            BuildHeaderButtons();

            statusBar = new MissionStatusBar { Dock = DockStyle.Bottom };

            rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = MissionPalette.Background
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, RailWidth));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0f));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            leftRail = BuildLeftRail();

            // Local 3D view (GPS-denied) and world map (GPS-available) share one host
            // panel and swap Visible between them - see Mission.cs's SetGpsAvailable. Never
            // both visible together: the two are the page's "Automatic Switching" pair, not
            // a split view.
            var centerHost = new Panel { Dock = DockStyle.Fill, BackColor = MissionPalette.Background };
            sceneView = new MissionSceneView { Dock = DockStyle.Fill };
            sceneView.SurvivorClicked += this.Scene_SurvivorSelected;
            worldMapView = new MissionWorldMapView { Dock = DockStyle.Fill, Visible = false };
            worldMapView.SurvivorClicked += this.Scene_SurvivorSelected;
            centerHost.Controls.Add(worldMapView);
            centerHost.Controls.Add(sceneView);

            drawer = BuildDrawer();
            drawer.Visible = false;

            rootLayout.Controls.Add(leftRail, 0, 0);
            rootLayout.Controls.Add(centerHost, 1, 0);
            rootLayout.Controls.Add(drawer, 2, 0);

            // Docked bottom-to-top so the status bar and header claim their strips before
            // the three-column body fills what remains.
            this.Controls.Add(rootLayout);
            this.Controls.Add(statusBar);
            this.Controls.Add(header);

            this.Load += new EventHandler(this.Mission_Load);
            this.ResumeLayout(false);
        }

        private void BuildHeaderButtons()
        {
            connectButton = new MissionActionButton { Text = "Connect", Size = new Size(96, 32), Outline = true };
            connectButton.Click += new EventHandler(this.connectButton_Click);

            drawerToggleButton = new MissionActionButton { Text = "Telem Log", Size = new Size(96, 32), Outline = true };
            drawerToggleButton.Click += new EventHandler(this.drawerToggleButton_Click);

            demoToggleButton = new MissionActionButton { Text = "Demo Mode", Size = new Size(104, 32), Outline = true };
            demoToggleButton.Click += new EventHandler(this.demoToggleButton_Click);

            abortButton = new MissionActionButton
            {
                Text = "Abort / E-Stop",
                Size = new Size(126, 32),
                FillColor = MissionPalette.Danger,
                TextColor = Color.White
            };
            abortButton.Click += new EventHandler(this.abortButton_Click);

            startMissionButton = new MissionActionButton
            {
                Text = "Start Mission",
                Size = new Size(114, 32),
                FillColor = MissionPalette.Ok,
                TextColor = Color.FromArgb(8, 20, 12)
            };
            startMissionButton.Click += new EventHandler(this.startMissionButton_Click);

            // AddRight places each new control to the left of the ones already added, so
            // the first call here ends up furthest right - see MissionHeader.OnLayout.
            header.AddRight(connectButton);
            header.AddRight(drawerToggleButton);
            header.AddRight(demoToggleButton);
            header.AddRight(abortButton);
            header.AddRight(startMissionButton);
        }

        private MissionSurface BuildLeftRail()
        {
            var rail = new MissionSurface { Dock = DockStyle.Fill, BorderEdges = Edges.Right };

            // Dual RGB + Thermal tiles side by side - the sensor fusion the deck calls
            // out as improving detection in smoke, fire and low light.
            var feedHeader = new SectionHeader("Live Feed") { Dock = DockStyle.Top };
            var feedRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 156,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = MissionPalette.Surface
            };
            feedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            feedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            feedRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            liveFeedRgb = new VideoFeedPanel { Dock = DockStyle.Fill, Margin = new Padding(10, 10, 5, 10), Caption = "RGB", CaptionSize = 8f };
            liveFeedThermal = new VideoFeedPanel { Dock = DockStyle.Fill, Margin = new Padding(5, 10, 10, 10), Caption = "THERMAL", CaptionSize = 8f };
            feedRow.Controls.Add(liveFeedRgb, 0, 0);
            feedRow.Controls.Add(liveFeedThermal, 1, 0);

            survivorsHeader = new SectionHeader("Survivors") { Dock = DockStyle.Top };
            survivorList = new SurvivorListView { Dock = DockStyle.Fill };
            survivorList.SurvivorSelected += this.Scene_SurvivorSelected;

            // Bottom-up dock order: header, feed strip and the survivors section header
            // pin to the top in reverse add order, leaving the list to fill what's left.
            rail.Controls.Add(survivorList);
            rail.Controls.Add(survivorsHeader);
            rail.Controls.Add(feedRow);
            rail.Controls.Add(feedHeader);

            return rail;
        }

        private MissionSurface BuildDrawer()
        {
            var d = new MissionSurface { Dock = DockStyle.Fill, BorderEdges = Edges.Left, AutoScroll = true };

            var telemetryHeader = new SectionHeader("Telemetry") { Dock = DockStyle.Top };
            telemetryReadout = new TelemetryReadout { Dock = DockStyle.Top };
            BuildTelemetryRows();
            telemetryReadout.Height = telemetryReadout.PreferredHeight;

            var logHeader = new SectionHeader("Mission Log") { Dock = DockStyle.Top, Height = 34 };
            var logToolbar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = MissionPalette.Surface };
            loadULogButton = new MissionActionButton { Text = "Load ULog...", Size = new Size(104, 28), Outline = true, Location = new Point(10, 8) };
            loadULogButton.Click += new EventHandler(this.loadULogButton_Click);
            logProgressRing = new MissionProgressRing { Location = new Point(DrawerWidth - 66, 4) };
            logToolbar.Controls.Add(loadULogButton);
            logToolbar.Controls.Add(logProgressRing);

            logBrowser = new MissionLogBrowser
            {
                Dock = DockStyle.Top,
                Height = 150,
                BorderStyle = BorderStyle.None,
                BackColor = MissionPalette.SurfaceRaised,
                ForeColor = MissionPalette.Text,
                LineColor = MissionPalette.Border,
                Font = MissionPalette.Small,
                Margin = new Padding(10)
            };
            logBrowser.FileSelected += this.LogBrowser_FileSelected;
            var logBrowserWrap = new Panel { Dock = DockStyle.Top, Height = 166, BackColor = MissionPalette.Surface, Padding = new Padding(10, 0, 10, 0) };
            logBrowser.Dock = DockStyle.Fill;
            logBrowserWrap.Controls.Add(logBrowser);

            ulogStatusLabel = MakeDrawerLabel();
            ulogStatusLabel.Dock = DockStyle.Top;
            ulogStatusLabel.Height = 20;
            logFileInfoLabel = MakeDrawerLabel();
            logFileInfoLabel.Text = "file info: ...";
            logFileInfoLabel.Dock = DockStyle.Top;
            logFileInfoLabel.Height = 18;
            logFilePathLabel = MakeDrawerLabel();
            logFilePathLabel.Text = "File path: ...";
            logFilePathLabel.Dock = DockStyle.Top;
            logFilePathLabel.Height = 18;

            var connectionHeader = new SectionHeader("Connection Health") { Dock = DockStyle.Top };
            connectionHealthPanel = new ConnectionHealthPanel { Dock = DockStyle.Top, Height = 150, BackColor = MissionPalette.Surface };

            var eventsHeader = new SectionHeader("Event Log") { Dock = DockStyle.Top };
            eventLog = new MissionEventLog { Dock = DockStyle.Top, Height = 220 };

            // Docked in reverse: everything above pins top-down in the order added below,
            // and the event log (added first) is left to fill the remaining height.
            d.Controls.Add(eventLog);
            d.Controls.Add(eventsHeader);
            d.Controls.Add(connectionHealthPanel);
            d.Controls.Add(connectionHeader);
            d.Controls.Add(logFilePathLabel);
            d.Controls.Add(logFileInfoLabel);
            d.Controls.Add(ulogStatusLabel);
            d.Controls.Add(logBrowserWrap);
            d.Controls.Add(logToolbar);
            d.Controls.Add(logHeader);
            d.Controls.Add(telemetryReadout);
            d.Controls.Add(telemetryHeader);

            return d;
        }

        private void BuildTelemetryRows()
        {
            telemetryReadout.AddRow("alt", "Altitude");
            telemetryReadout.AddRow("climb", "Climb rate");
            telemetryReadout.AddRow("speed", "Ground speed");
            telemetryReadout.AddRow("roll", "Roll");
            telemetryReadout.AddRow("pitch", "Pitch");
            telemetryReadout.AddRow("yaw", "Yaw");
            telemetryReadout.AddRow("volts", "Battery volts");
            telemetryReadout.AddRow("current", "Current");
            telemetryReadout.AddRow("batttemp", "Battery temp.");
            telemetryReadout.AddRow("landed", "Landed state");
            telemetryReadout.AddRow("timeinair", "Time in air");
            telemetryReadout.AddRow("timesincearm", "Time since arm");
        }

        private static Label MakeDrawerLabel()
        {
            return new Label
            {
                Text = "",
                ForeColor = MissionPalette.TextFaint,
                BackColor = MissionPalette.Surface,
                Font = MissionPalette.Small,
                Padding = new Padding(10, 2, 10, 0),
                Tag = "custom"
            };
        }

        #endregion
    }
}
