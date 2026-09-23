using System;
using System.Drawing;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using MissionPlanner.Utilities;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// GPS-available-mode counterpart to MissionSceneView: the drone, survivors and
    /// hazards as real lat/lon markers on an actual map, using GMap.NET - the same map
    /// control FlightData/FlightPlanner already use elsewhere in this app, rather than a
    /// second mapping dependency. MissionScene.GpsOrigin anchors the local metres the rest
    /// of the dashboard works in onto the real world - see MissionScene.FormatTag.
    ///
    /// GCSViews\Mission.cs swaps this in for the centre pane whenever
    /// MissionScene.GpsAvailable is true and swaps MissionSceneView back in the moment it
    /// drops - the "Automatic Switching" between GPS-enabled and GPS-denied navigation the
    /// SIH26177 architecture describes. Both views share one MissionScene, so nothing
    /// about a survivor or hazard changes crossing between them - only how its position is
    /// labelled and drawn.
    /// </summary>
    public class MissionWorldMapView : Control
    {
        private readonly GMapControl map;
        private readonly GMapOverlay hazardsOverlay = new GMapOverlay("mission_hazards");
        private readonly GMapOverlay survivorsOverlay = new GMapOverlay("mission_survivors");
        private readonly GMapOverlay droneOverlay = new GMapOverlay("mission_drone");
        private readonly Label hud;
        private readonly Label hint;

        private MissionScene scene;
        private bool everFocused;

        /// <summary>Raised with the id of a survivor marker the operator clicked.</summary>
        public event Action<int> SurvivorClicked;

        public MissionWorldMapView()
        {
            BackColor = MissionPalette.Background;

            map = new GMapControl
            {
                Dock = DockStyle.Fill,
                MapProvider = OpenStreetMapProvider.Instance,
                CacheLocation = Settings.GetDataDirectory() + "gmapcache" + System.IO.Path.DirectorySeparatorChar,
                MinZoom = 2,
                MaxZoom = 21,
                Zoom = 19,
                DragButton = MouseButtons.Left,
                ShowTileGridLines = false,
                MapScaleInfoEnabled = false,
                DisableFocusOnMouseEnter = true
            };
            map.Overlays.Add(hazardsOverlay);
            map.Overlays.Add(survivorsOverlay);
            map.Overlays.Add(droneOverlay);
            map.OnMarkerClick += Map_OnMarkerClick;
            Controls.Add(map);

            hud = new Label
            {
                AutoSize = true,
                BackColor = Color.FromArgb(220, MissionPalette.Surface),
                ForeColor = MissionPalette.TextDim,
                Font = MissionPalette.Caps,
                Padding = new Padding(10, 6, 10, 6),
                Location = new Point(12, 12),
                Text = "GPS AVAILABLE  ·  WORLD MAP",
                Tag = "custom"
            };

            hint = new Label
            {
                AutoSize = true,
                BackColor = hud.BackColor,
                ForeColor = MissionPalette.TextFaint,
                Font = MissionPalette.Small,
                Padding = new Padding(10, 4, 10, 4),
                Location = new Point(12, 40),
                Text = "Drag to pan · wheel to zoom · click a survivor pin",
                Tag = "custom"
            };

            Controls.Add(hud);
            Controls.Add(hint);
            hud.BringToFront();
            hint.BringToFront();
        }

        public void Bind(MissionScene s)
        {
            scene = s;
        }

        /// <summary>
        /// Recentres and zooms to the arena - call once right when this view is switched
        /// into visibility (Mission.cs does), not every tick: a per-tick recentre would
        /// fight the operator dragging or zooming the map by hand.
        /// </summary>
        public void FocusOnScene()
        {
            if (scene?.GpsOrigin == null)
                return;

            everFocused = true;
            var (lat, lon) = scene.GpsOrigin.Value.ToLatLon(
                new PointF(scene.ArenaSize / 2f, scene.ArenaSize / 2f));
            map.Position = new PointLatLng(lat, lon);
            map.Zoom = 19;
        }

        /// <summary>
        /// Rebuilds the marker overlays from the bound scene. Cheap enough to call every
        /// tick - a handful of markers, no tile work.
        /// </summary>
        public void RefreshMarkers()
        {
            if (scene?.GpsOrigin == null)
                return;

            if (!everFocused)
                FocusOnScene();

            var origin = scene.GpsOrigin.Value;

            droneOverlay.Markers.Clear();
            if (scene.HasDrone)
            {
                var (lat, lon) = origin.ToLatLon(new PointF(scene.Drone.X, scene.Drone.Y));
                droneOverlay.Markers.Add(new GMarkerGoogle(new PointLatLng(lat, lon), GMarkerGoogleType.arrow)
                {
                    Bearing = scene.DroneYawDeg,
                    ToolTipMode = MarkerTooltipMode.OnMouseOver,
                    ToolTipText = scene.DroneArmed ? "Drone · armed" : "Drone · disarmed"
                });
            }

            survivorsOverlay.Markers.Clear();
            foreach (var s in scene.Survivors)
            {
                var (lat, lon) = origin.ToLatLon(s.Position);
                survivorsOverlay.Markers.Add(new GMarkerGoogle(new PointLatLng(lat, lon), GMarkerGoogleType.orange_dot)
                {
                    Tag = s.Id,
                    ToolTipMode = MarkerTooltipMode.OnMouseOver,
                    ToolTipText = s.Name + "  ·  " + lat.ToString("0.00000") + ", " + lon.ToString("0.00000")
                });
            }

            hazardsOverlay.Markers.Clear();
            foreach (var h in scene.Hazards)
            {
                var (lat, lon) = origin.ToLatLon(h.Position);
                hazardsOverlay.Markers.Add(new GMarkerGoogle(new PointLatLng(lat, lon), MarkerTypeFor(h.Type))
                {
                    ToolTipMode = MarkerTooltipMode.OnMouseOver,
                    ToolTipText = HazardVisuals.Label(h.Type) + " · " + h.Priority + " priority"
                });
            }
        }

        private static GMarkerGoogleType MarkerTypeFor(HazardType type)
        {
            switch (type)
            {
                case HazardType.Flood: return GMarkerGoogleType.lightblue_dot;
                case HazardType.Electrical: return GMarkerGoogleType.yellow_dot;
                case HazardType.Structural: return GMarkerGoogleType.purple_dot;
                default: return GMarkerGoogleType.red_dot; // Fire
            }
        }

        private void Map_OnMarkerClick(GMapMarker item, object mouseEventArgs)
        {
            if (item?.Tag is int id)
                SurvivorClicked?.Invoke(id);
        }
    }
}
