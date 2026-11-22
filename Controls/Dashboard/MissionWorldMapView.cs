using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using MissionPlanner.Utilities;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// GPS-enabled-mode counterpart to MissionSceneView: the drone, survivors and
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
        private readonly GMapOverlay coverageOverlay = new GMapOverlay("mission_coverage");
        private readonly GMapOverlay siteOverlay = new GMapOverlay("mission_site");
        private readonly GMapOverlay routesOverlay = new GMapOverlay("mission_routes");
        private readonly GMapOverlay hazardsOverlay = new GMapOverlay("mission_hazards");
        private readonly GMapOverlay survivorsOverlay = new GMapOverlay("mission_survivors");
        private readonly GMapOverlay droneOverlay = new GMapOverlay("mission_drone");
        private readonly Label hud;
        private readonly Label hint;
        private readonly Label surveyInfo;
        private readonly Label routeInfo;
        private readonly Label rtlInfo;
        private readonly Label tileNotice;
        private readonly Timer tileCheckTimer;
        private bool tileEverLoaded;

        private MissionScene scene;
        private bool everFocused;

        // What the site/route overlays currently show, so RefreshMarkers only rebuilds
        // polylines when something actually changed - see RefreshSite/RefreshRoutes.
        private int shownWallCount = -1;
        private Wall shownLastWall;
        private PointF shownHome, shownEntry;
        private GeoOrigin shownOrigin, shownRouteOrigin, shownCoverageOrigin;
        private int shownHazardCount = -1;
        private List<PointF> shownPattern;
        private int shownTrailCount = -1;
        private double shownCoverageZoom = -1;
        private Detection shownSelected;
        private List<PointF> shownRescue, shownRtl;
        private bool shownRtlActive;

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
                DisableFocusOnMouseEnter = true,
                // Themed rather than GMap.NET's default white/grey: without a live internet
                // connection (a venue's wifi, say) no tiles ever arrive and this is what
                // fills the whole control - it should still look deliberate, not broken.
                // Overlays (drone/survivor/hazard markers) render regardless of whether any
                // tile ever loads, so the fake data is visible either way.
                EmptyTileColor = MissionPalette.Background
            };
            // Bottom to top: survey coverage, the site (area, lanes, keep-out zones, walls),
            // then the routes, then every pin above them.
            map.Overlays.Add(coverageOverlay);
            map.Overlays.Add(siteOverlay);
            map.Overlays.Add(routesOverlay);
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
                Text = "GPS ENABLED  ·  WORLD MAP",
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

            // Legend chips for the survey and the two paths drawn on the map - each doubles
            // as its own readout, and hides itself when there's nothing to describe.
            surveyInfo = new Label
            {
                AutoSize = true,
                BackColor = hud.BackColor,
                ForeColor = MissionPalette.Text,
                Font = MissionPalette.SmallBold,
                Padding = new Padding(10, 4, 10, 4),
                Location = new Point(12, 70),
                Visible = false,
                Tag = "custom"
            };

            routeInfo = new Label
            {
                AutoSize = true,
                BackColor = hud.BackColor,
                ForeColor = MissionPalette.Route,
                Font = MissionPalette.SmallBold,
                Padding = new Padding(10, 4, 10, 4),
                Location = new Point(12, 70),
                Visible = false,
                Tag = "custom"
            };

            rtlInfo = new Label
            {
                AutoSize = true,
                BackColor = hud.BackColor,
                ForeColor = MissionPalette.Accent,
                Font = MissionPalette.SmallBold,
                Padding = new Padding(10, 4, 10, 4),
                Location = new Point(12, 96),
                Visible = false,
                Tag = "custom"
            };

            // Shown only if no tile has actually loaded a few seconds after this view
            // becomes visible - a venue with no usable internet, say - so a blank basemap
            // reads as "tiles unavailable, markers are still accurate" rather than broken.
            // Hidden the moment a tile does come in, from OnAnyTileLoaded below.
            tileNotice = new Label
            {
                AutoSize = true,
                BackColor = Color.FromArgb(230, MissionPalette.SurfaceRaised),
                ForeColor = MissionPalette.Warning,
                Font = MissionPalette.SmallBold,
                Padding = new Padding(10, 6, 10, 6),
                Text = "Map tiles need internet — markers below are still accurate",
                Visible = false,
                Tag = "custom"
            };

            map.OnTileLoadComplete += OnAnyTileLoaded;
            tileCheckTimer = new Timer { Interval = 4000 };
            tileCheckTimer.Tick += (s, e) =>
            {
                tileCheckTimer.Stop();
                if (!tileEverLoaded)
                    tileNotice.Visible = true;
            };

            Controls.Add(hud);
            Controls.Add(hint);
            Controls.Add(tileNotice);
            Controls.Add(surveyInfo);
            Controls.Add(routeInfo);
            Controls.Add(rtlInfo);
            hud.BringToFront();
            hint.BringToFront();
            surveyInfo.BringToFront();
            routeInfo.BringToFront();
            rtlInfo.BringToFront();
            tileNotice.BringToFront();
        }

        private void OnAnyTileLoaded(long elapsedMilliseconds)
        {
            tileEverLoaded = true;
            RunOnUiThread(() => tileNotice.Visible = false);
        }

        private void RunOnUiThread(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                if (InvokeRequired)
                    BeginInvoke(action);
                else
                    action();
            }
            catch (ObjectDisposedException)
            {
                // control torn down between the guard above and the marshalled call
            }
        }

        public void Bind(MissionScene s)
        {
            scene = s;
        }

        /// <summary>
        /// Recentres and zooms to fit the site - call once right when this view is switched
        /// into visibility (Mission.cs does), not every tick: a per-tick recentre would
        /// fight the operator dragging or zooming the map by hand.
        /// </summary>
        public void FocusOnScene()
        {
            if (scene?.GpsOrigin == null)
                return;

            var origin = scene.GpsOrigin.Value;

            // Not laid out yet (it has only just been made visible): a fit now would
            // compute against a zero-size control and zoom out to the whole planet, where
            // the drone looks frozen. Centre on the site and let the next RefreshMarkers
            // tick - by which point the control has its real size - do the actual fit.
            if (map.Width < 100 || map.Height < 100)
            {
                map.Position = ToLatLng(origin, new PointF(scene.ArenaSize / 2f, scene.ArenaSize / 2f));
                map.Zoom = 18;
                everFocused = false;
                return;
            }

            everFocused = true;
            var sw = ToLatLng(origin, PointF.Empty);
            var ne = ToLatLng(origin, new PointF(scene.ArenaSize, scene.ArenaSize));

            // A margin so the site's edge isn't the screen's edge; capped at 19 so a small
            // indoor footprint doesn't zoom in past street level.
            var padLat = (ne.Lat - sw.Lat) * 0.15;
            var padLng = (ne.Lng - sw.Lng) * 0.15;
            var fit = RectLatLng.FromLTRB(sw.Lng - padLng, ne.Lat + padLat, ne.Lng + padLng, sw.Lat - padLat);
            if (!map.SetZoomToFitRect(fit))
            {
                map.Position = ToLatLng(origin, new PointF(scene.ArenaSize / 2f, scene.ArenaSize / 2f));
                map.Zoom = 19;
            }
            if (map.Zoom > 19)
                map.Zoom = 19;
            else if (map.Zoom < 15)
                map.Zoom = 15; // never so far out that a 150 m site is a few pixels

            if (!tileEverLoaded)
            {
                tileNotice.Visible = false;
                tileCheckTimer.Stop();
                tileCheckTimer.Start();
            }
        }

        /// <summary>
        /// Rebuilds the marker, wall and route overlays from the bound scene. Cheap enough
        /// to call every tick - a handful of markers and polylines, no tile work.
        /// </summary>
        public void RefreshMarkers()
        {
            if (scene?.GpsOrigin == null)
                return;

            if (!everFocused)
                FocusOnScene();

            var origin = scene.GpsOrigin.Value;

            RefreshCoverage(origin);
            RefreshSite(origin);
            RefreshRoutes(origin);
            RefreshSurveyChip();
            LayoutChips();

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
                var selected = s.Id == scene.SelectedSurvivorId;
                survivorsOverlay.Markers.Add(new GMarkerGoogle(new PointLatLng(lat, lon),
                    selected ? GMarkerGoogleType.green : GMarkerGoogleType.orange_dot)
                {
                    Tag = s.Id,
                    ToolTipMode = selected ? MarkerTooltipMode.Always : MarkerTooltipMode.OnMouseOver,
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

        private static readonly Pen AreaPen = new Pen(Color.FromArgb(150, MissionPalette.Accent), 1.5f)
            { DashStyle = DashStyle.Dash };
        private static readonly Color AreaFill = Color.FromArgb(14, MissionPalette.Accent);
        private static readonly Pen LanePen = new Pen(Color.FromArgb(110, MissionPalette.Text), 1.2f)
            { DashStyle = DashStyle.Dot };
        private static readonly Pen TrailPen = new Pen(Color.FromArgb(220, MissionPalette.Accent), 2f)
            { LineJoin = LineJoin.Round };
        private static readonly Color SwathColor = Color.FromArgb(46, MissionPalette.Accent);

        private static readonly Pen WallPen = new Pen(Color.FromArgb(170, MissionPalette.TextDim), 2f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };

        // Rescue route: a soft halo under a solid line with direction chevrons (GMapRoute
        // draws those for DashStyle.Custom), so it reads over both tiles and a blank map.
        private static readonly Pen RouteHaloPen = new Pen(Color.FromArgb(70, MissionPalette.Route), 10f)
            { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        private static readonly Pen RoutePen = new Pen(MissionPalette.Route, 3f)
            { LineJoin = LineJoin.Round, DashStyle = DashStyle.Custom };

        // RTL: dashed while it's only the standing "if recalled now" path, solid with
        // chevrons once the vehicle is actually flying it.
        private static readonly Pen RtlStandbyPen = new Pen(Color.FromArgb(200, MissionPalette.Accent), 2.5f)
            { LineJoin = LineJoin.Round, DashStyle = DashStyle.Dash };
        private static readonly Pen RtlActiveHaloPen = new Pen(Color.FromArgb(70, MissionPalette.Warning), 10f)
            { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        private static readonly Pen RtlActivePen = new Pen(MissionPalette.Warning, 3f)
            { LineJoin = LineJoin.Round, DashStyle = DashStyle.Custom };

        /// <summary>
        /// Outdoor survey only: the ground the camera has swept so far, as a translucent
        /// band one lane wide along the flown track - so the lawnmower passes visibly "mow"
        /// the area - with the track itself on top. Rebuilt when the track grows or the
        /// zoom changes (the band's width is in pixels).
        /// </summary>
        private void RefreshCoverage(GeoOrigin origin)
        {
            var pattern = scene.SurveyPattern;
            var trailCount = pattern == null ? 0 : scene.Trail.Count;
            if (trailCount == shownTrailCount && map.Zoom == shownCoverageZoom &&
                origin.Lat == shownCoverageOrigin.Lat && origin.Lon == shownCoverageOrigin.Lon)
                return;

            shownTrailCount = trailCount;
            shownCoverageZoom = map.Zoom;
            shownCoverageOrigin = origin;

            ClearOverlay(coverageOverlay);
            if (trailCount < 2 || pattern.Count < 3)
                return;

            var track = new List<PointLatLng>(trailCount);
            foreach (var t in scene.Trail)
                track.Add(ToLatLng(origin, new PointF(t.X, t.Y)));

            var laneSpacing = Math.Abs(pattern[2].X - pattern[0].X);
            var metresPerPixel = map.MapProvider.Projection.GetGroundResolution((int)Math.Round(map.Zoom), origin.Lat);
            var swathPx = (float)Math.Max(2, laneSpacing / metresPerPixel);

            coverageOverlay.Routes.Add(new GMapRoute(track, "swath")
            {
                Stroke = new Pen(SwathColor, swathPx) { LineJoin = LineJoin.Round }
            });
            coverageOverlay.Routes.Add(new GMapRoute(track, "track") { Stroke = (Pen)TrailPen.Clone() });
        }

        /// <summary>
        /// The site itself: survey area and planned lawnmower lanes (outdoors), hazard
        /// keep-out zones the rescue routes bend around, mapped walls, plus home and entry
        /// pins - what gives the route lines something to be read against, especially with
        /// no basemap tiles.
        /// </summary>
        private void RefreshSite(GeoOrigin origin)
        {
            // Walls and hazards only ever get added or wiped wholesale, so counts + last
            // wall + the pins catch every change without redrawing it all every tick.
            var lastWall = scene.Walls.Count > 0 ? scene.Walls[scene.Walls.Count - 1] : default(Wall);
            if (scene.Walls.Count == shownWallCount && lastWall.A == shownLastWall.A && lastWall.B == shownLastWall.B &&
                scene.Hazards.Count == shownHazardCount && scene.SurveyPattern == shownPattern &&
                scene.RtlHome == shownHome && scene.Entry == shownEntry && origin.Lat == shownOrigin.Lat &&
                origin.Lon == shownOrigin.Lon)
                return;

            shownWallCount = scene.Walls.Count;
            shownLastWall = lastWall;
            shownHazardCount = scene.Hazards.Count;
            shownPattern = scene.SurveyPattern;
            shownHome = scene.RtlHome;
            shownEntry = scene.Entry;
            shownOrigin = origin;

            ClearOverlay(siteOverlay);
            siteOverlay.Markers.Clear();

            if (scene.SurveyPattern != null)
            {
                var size = scene.ArenaSize;
                siteOverlay.Polygons.Add(new GMapPolygon(new List<PointLatLng>
                {
                    ToLatLng(origin, new PointF(0, 0)), ToLatLng(origin, new PointF(size, 0)),
                    ToLatLng(origin, new PointF(size, size)), ToLatLng(origin, new PointF(0, size))
                }, "survey_area")
                {
                    Stroke = (Pen)AreaPen.Clone(),
                    Fill = new SolidBrush(AreaFill),
                    IsHitTestVisible = false
                });

                var lanes = new List<PointLatLng>(scene.SurveyPattern.Count);
                foreach (var p in scene.SurveyPattern)
                    lanes.Add(ToLatLng(origin, p));
                siteOverlay.Routes.Add(new GMapRoute(lanes, "lanes") { Stroke = (Pen)LanePen.Clone() });
            }

            foreach (var h in scene.Hazards)
            {
                var colour = HazardVisuals.Color(h.Type);
                siteOverlay.Polygons.Add(new GMapPolygon(Circle(origin, h.Position, h.HazardRadius), "keepout")
                {
                    Stroke = new Pen(Color.FromArgb(170, colour), 1.5f),
                    Fill = new SolidBrush(Color.FromArgb(45, colour)),
                    IsHitTestVisible = false
                });
            }

            foreach (var w in scene.Walls)
                siteOverlay.Routes.Add(new GMapRoute(new[] { ToLatLng(origin, w.A), ToLatLng(origin, w.B) }, "wall")
                    { Stroke = (Pen)WallPen.Clone() });

            siteOverlay.Markers.Add(new GMarkerGoogle(ToLatLng(origin, scene.RtlHome), GMarkerGoogleType.blue_small)
            {
                ToolTipMode = MarkerTooltipMode.OnMouseOver,
                ToolTipText = "Home · RTL / dock"
            });
            siteOverlay.Markers.Add(new GMarkerGoogle(ToLatLng(origin, scene.Entry), GMarkerGoogleType.white_small)
            {
                ToolTipMode = MarkerTooltipMode.OnMouseOver,
                ToolTipText = scene.SurveyPattern != null
                ? "Rescue staging point · ground routes start here"
                : "Entry · rescue routes start here"
            });
        }

        private static List<PointLatLng> Circle(GeoOrigin origin, PointF centre, float radius)
        {
            const int Segments = 32;
            var points = new List<PointLatLng>(Segments);
            for (var i = 0; i < Segments; i++)
            {
                var a = i * 2 * Math.PI / Segments;
                points.Add(ToLatLng(origin, new PointF(centre.X + radius * (float)Math.Cos(a),
                                                       centre.Y + radius * (float)Math.Sin(a))));
            }
            return points;
        }

        /// <summary>Outdoor survey progress: lanes, area covered, and what's been tagged so far.</summary>
        private void RefreshSurveyChip()
        {
            if (scene.SurveyPattern == null)
            {
                SetChip(surveyInfo, null);
                return;
            }

            SetChip(surveyInfo, "LAWNMOWER SURVEY  ·  " + scene.SurveyPattern.Count / 2 + " lanes  ·  " +
                                (int)Math.Round(scene.Explored.Fraction * 100) + "% covered  ·  " +
                                scene.Survivors.Count + " victims, " + scene.Hazards.Count + " hazards tagged");
        }

        /// <summary>Stacks whichever legend chips are showing under the hint, with no gaps.</summary>
        private void LayoutChips()
        {
            var y = 70;
            foreach (var chip in new[] { surveyInfo, routeInfo, rtlInfo })
            {
                if (!chip.Visible)
                    continue;
                chip.Top = y;
                y += chip.Height + 4;
            }
        }

        /// <summary>The selected survivor's shortest safe route and the drone's RTL path, plus their legend chips.</summary>
        private void RefreshRoutes(GeoOrigin origin)
        {
            // The planner hands back a fresh list whenever a path changes, so reference
            // equality is enough to skip rebuilding polylines that haven't moved.
            var selected = scene.SelectedSurvivor;
            if (selected == shownSelected && selected?.Route == shownRescue && scene.RtlRoute == shownRtl &&
                scene.RtlActive == shownRtlActive && origin.Lat == shownRouteOrigin.Lat && origin.Lon == shownRouteOrigin.Lon)
                return;

            shownSelected = selected;
            shownRescue = selected?.Route;
            shownRtl = scene.RtlRoute;
            shownRtlActive = scene.RtlActive;
            shownRouteOrigin = origin;

            ClearOverlay(routesOverlay);

            if (selected?.Route != null && selected.Route.Count >= 2)
            {
                AddPath(origin, selected.Route, "rescue_halo", RouteHaloPen);
                AddPath(origin, selected.Route, "rescue", RoutePen);
                SetChip(routeInfo, "SHORTEST SAFE ROUTE  ·  " + (scene.SurveyPattern != null ? "Staging" : "Entry") +
                                   " → " + selected.Name + "  ·  " +
                                   selected.RouteLength.ToString("0.0") + " m");
            }
            else
            {
                SetChip(routeInfo, selected != null ? "No safe route to " + selected.Name + " on the known map yet" : null);
            }

            var rtl = scene.RtlRoute;
            if (rtl != null && rtl.Count >= 2)
            {
                if (scene.RtlActive)
                {
                    AddPath(origin, rtl, "rtl_halo", RtlActiveHaloPen);
                    AddPath(origin, rtl, "rtl", RtlActivePen);
                }
                else
                {
                    AddPath(origin, rtl, "rtl", RtlStandbyPen);
                }

                SetChip(rtlInfo, (scene.RtlActive ? "RTL ACTIVE  ·  returning home" : "RTL PATH  ·  if recalled now") +
                                 "  ·  " + PathLength(rtl).ToString("0.0") + " m");
                rtlInfo.ForeColor = scene.RtlActive ? MissionPalette.Warning : MissionPalette.Accent;
            }
            else
            {
                SetChip(rtlInfo, null);
            }
        }

        private void AddPath(GeoOrigin origin, List<PointF> path, string name, Pen pen)
        {
            var points = new List<PointLatLng>(path.Count);
            foreach (var p in path)
                points.Add(ToLatLng(origin, p));
            // Each route gets its own pen: GMapRoute may flip a pen's DashStyle while
            // drawing, which would otherwise stick on the shared template.
            routesOverlay.Routes.Add(new GMapRoute(points, name) { Stroke = (Pen)pen.Clone() });
        }

        /// <summary>
        /// Removes and disposes an overlay's routes and polygons - neither GMapOverlay nor
        /// the shapes themselves free their pens/brushes/paths on their own.
        /// </summary>
        private static void ClearOverlay(GMapOverlay overlay)
        {
            var routes = new List<GMapRoute>(overlay.Routes);
            overlay.Routes.Clear();
            foreach (var r in routes)
            {
                r.Stroke.Dispose();
                r.Dispose();
            }

            var polygons = new List<GMapPolygon>(overlay.Polygons);
            overlay.Polygons.Clear();
            foreach (var p in polygons)
            {
                p.Stroke.Dispose();
                p.Fill.Dispose();
                p.Dispose();
            }
        }

        /// <summary>Shows <paramref name="text"/> in a chip, or hides it for null; skips no-op updates so it doesn't flicker at tick rate.</summary>
        private static void SetChip(Label chip, string text)
        {
            var show = text != null;
            if (show && chip.Text != text)
                chip.Text = text;
            if (chip.Visible != show)
                chip.Visible = show;
        }

        private static float PathLength(List<PointF> path)
        {
            float d = 0;
            for (var i = 1; i < path.Count; i++)
            {
                var dx = path[i].X - path[i - 1].X;
                var dy = path[i].Y - path[i - 1].Y;
                d += (float)Math.Sqrt(dx * dx + dy * dy);
            }
            return d;
        }

        private static PointLatLng ToLatLng(GeoOrigin origin, PointF local)
        {
            var (lat, lon) = origin.ToLatLon(local);
            return new PointLatLng(lat, lon);
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            tileNotice.Location = new Point(12, Height - tileNotice.Height - 12);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tileCheckTimer.Stop();
                tileCheckTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
