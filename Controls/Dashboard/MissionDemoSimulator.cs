using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace MissionPlanner.Controls.Dashboard
{
    public enum DemoPhase
    {
        Idle,
        Arming,
        Takeoff,
        Search,
        Exit,
        Landing,
        Returning,
        Complete,

        /// <summary>Autonomous return to the docking/charging station on low battery - not
        /// a mission abort, the search resumes once recharged.</summary>
        DockingReturn,

        /// <summary>Landed and charging at the dock; disarmed, mission not yet complete.</summary>
        Charging
    }

    /// <summary>
    /// One tick of fabricated vehicle state, shaped like the fields GCSViews.Mission
    /// normally pulls off MainV2.comPort.MAV.cs.
    /// </summary>
    public class DemoSnapshot
    {
        public DemoPhase Phase;
        public string Mode;
        public bool Armed;

        public int BatteryPercent;
        public double BatteryVolts;
        public double CurrentAmps;
        public double BatteryTempC;

        public double AltitudeM;
        public double ClimbRate;
        public double GroundSpeed;
        public double RollDeg;
        public double PitchDeg;
        public double YawDeg;

        public TimeSpan? MissionTime;

        public double LinkQuality;
        public int BytesPerSecond;
        public double LatencyMs;
    }

    /// <summary>
    /// Fabricates a whole SIH26177 (Team Asunama) run for the Mission page's demo mode: a
    /// GPS-denied indoor search that discovers survivors and multi-class hazards (fire,
    /// flood, exposed electrical, structural) as it flies, plus a scripted low-battery
    /// autonomous docking/recharge/resume cycle - the "Mission Continuity & Auto Charging"
    /// loop from the architecture, not just a single flight to empty. The drone takes off
    /// from the pad, flies a scripted room-by-room search, and everything it "sees" (floor,
    /// walls, survivors, hazards within sensor range and line of sight) is revealed into
    /// the shared MissionScene - so the map builds up exactly as a live map feed would. It
    /// ends by leaving through the exit and landing.
    ///
    /// Abort flies home over the known map using the same planner as the rescue routes,
    /// then lands on the pad. It never touches MainV2.comPort; demo mode is a pure display
    /// overlay (see GCSViews\Mission.cs).
    /// </summary>
    public class MissionDemoSimulator
    {
        private const float CruiseAltitude = 1.6f;
        private const float CruiseSpeed = 0.62f;
        private const float ReturnSpeed = 0.9f;
        private const float TakeoffSeconds = 5f;
        private const float LandSeconds = 5f;
        private const float ArmSeconds = 2f;
        private const float SensorRange = 3.2f;
        private const float SurvivorRange = 2.2f;
        private const float HazardRange = 3.6f;
        private const float WallPiece = 0.25f;

        // Mission-continuity / auto-charging cycle: trigger a scripted dock-and-recharge
        // once per run when the pack crosses DockTriggerBatteryPercent mid-search, then
        // resume the search from exactly where it left off once charged back up.
        private const double DockTriggerBatteryPercent = 55.0;
        private const double ResumeBatteryPercent = 85.0;
        private const double ChargeRatePercentPerSecond = 2.5;

        private readonly MissionScene scene;
        private readonly Func<double> clock;
        private readonly Random rng = new Random(20260912);
        private readonly List<MissionEvent> pendingEvents = new List<MissionEvent>();

        // Ground truth the drone discovers.
        private readonly List<Wall> truthWalls = new List<Wall>();
        private readonly List<PointF> truthSurvivors = new List<PointF>();
        private readonly List<(PointF Position, HazardType Type, HazardPriority Priority)> truthHazards =
            new List<(PointF, HazardType, HazardPriority)>();
        private readonly List<bool[]> revealedPieces = new List<bool[]>();

        private List<Waypoint> route;
        private int waypointIndex;
        private double holdUntil;

        // Saved so a dock-and-recharge cycle resumes the search leg it interrupted,
        // instead of restarting the whole route from the first waypoint.
        private List<Waypoint> savedRoute;
        private int savedWaypointIndex;
        private bool dockCycleDone;
        private bool resumingFromDock;

        private DemoPhase phase = DemoPhase.Idle;
        private double phaseStart;
        private double lastSample;
        private double takeoffAt = -1;

        private PointF pos;
        private PointF velocity;
        private float altitude;
        private float yawDeg;
        private float lastAltitude;
        private float landFromAltitude;

        /// <summary>True while the current Landing phase is a dock-and-recharge stop rather than mission end.</summary>
        private bool landingForDock;

        private double battery = 97;
        private double tempC = 27;
        private double linkQuality = 0.97;
        private double latencyMs = 24;
        private double bandwidth = 4300;

        private struct Waypoint
        {
            public PointF P;
            public float HoldSeconds;
            public Waypoint(float x, float y, float hold = 0)
            {
                P = new PointF(x, y);
                HoldSeconds = hold;
            }
        }

        public MissionDemoSimulator(MissionScene scene, Func<double> clock = null)
        {
            this.scene = scene;
            if (clock == null)
            {
                var sw = Stopwatch.StartNew();
                clock = () => sw.Elapsed.TotalSeconds;
            }
            this.clock = clock;

            BuildArena();
        }

        public DemoPhase Phase
        {
            get { return phase; }
        }

        public bool IsFlying
        {
            get { return phase == DemoPhase.Takeoff || phase == DemoPhase.Search || phase == DemoPhase.Exit ||
                         phase == DemoPhase.Landing || phase == DemoPhase.Returning || phase == DemoPhase.DockingReturn; }
        }

        /// <summary>Bumped whenever walls, floor or detections change, so the page knows when to re-plan routes.</summary>
        public int MapVersion { get; private set; }

        /// <summary>Events raised since the last call, oldest first.</summary>
        public List<MissionEvent> DrainEvents()
        {
            var copy = new List<MissionEvent>(pendingEvents);
            pendingEvents.Clear();
            return copy;
        }

        /// <summary>Back to a full pack on the launch pad with nothing mapped.</summary>
        public void Reset()
        {
            scene.Clear();
            for (var i = 0; i < revealedPieces.Count; i++)
                Array.Clear(revealedPieces[i], 0, revealedPieces[i].Length);

            phase = DemoPhase.Idle;
            phaseStart = lastSample = clock();
            takeoffAt = -1;
            pos = scene.LaunchPad;
            velocity = PointF.Empty;
            altitude = lastAltitude = 0;
            yawDeg = 0;
            battery = 97;
            tempC = 27;
            route = null;
            waypointIndex = 0;
            savedRoute = null;
            savedWaypointIndex = 0;
            dockCycleDone = false;
            resumingFromDock = false;
            pendingEvents.Clear();

            scene.HasDrone = true;
            UpdateDroneInScene();
            Reveal();
            MapVersion++;
        }

        /// <summary>
        /// Instant fake dataset for showing the finished picture without waiting through a
        /// flight: the whole map explored, every survivor and hazard already reported, the
        /// drone parked disarmed on the pad. This is what "Demo Mode" shows the moment it's
        /// switched on. "Start Mission" still throws all of this away and flies the real
        /// scripted discovery from a blank map - see StartMission.
        /// </summary>
        public void Preview()
        {
            Reset();

            for (var cy = 0; cy < scene.Explored.Size; cy++)
                for (var cx = 0; cx < scene.Explored.Size; cx++)
                    scene.Explored.Mark(cx, cy);

            for (var w = 0; w < revealedPieces.Count; w++)
                for (var i = 0; i < revealedPieces[w].Length; i++)
                    revealedPieces[w][i] = true;
            RebuildSceneWalls();

            foreach (var p in truthSurvivors)
            {
                scene.Survivors.Add(new Detection
                {
                    Id = scene.Survivors.Count + 1,
                    Kind = DetectionKind.Survivor,
                    Position = p,
                    DetectedAt = TimeSpan.Zero
                });
            }

            foreach (var h in truthHazards)
            {
                scene.Hazards.Add(new Detection
                {
                    Id = scene.Hazards.Count + 1,
                    Kind = DetectionKind.Hazard,
                    Type = h.Type,
                    Priority = h.Priority,
                    Position = h.Position,
                    DetectedAt = TimeSpan.Zero,
                    HazardRadius = 1.4f
                });
            }

            // Plan every survivor's rescue route up front and highlight the first one, so
            // the "best path" is already drawn the moment Demo Mode switches on - nothing
            // to click before it's visible.
            if (scene.Survivors.Count > 0)
            {
                var planner = RescuePathPlanner.Build(scene);
                foreach (var s in scene.Survivors)
                    s.Route = planner.Plan(scene.Entry, s.Position);
                scene.SelectedSurvivorId = scene.Survivors[0].Id;
            }

            MapVersion++;
            Emit(MissionEventKind.Info, "Preview · full map, " + truthSurvivors.Count + " survivors and " +
                                        truthHazards.Count + " hazards shown for demonstration");
        }

        public void StartMission()
        {
            if (phase != DemoPhase.Idle && phase != DemoPhase.Complete)
                return;

            // Always a clean slate - if Preview() populated a fake finished map, the real
            // scripted flight still discovers everything itself rather than flying over
            // ground already marked explored.
            Reset();
            SetPhase(DemoPhase.Arming);
            Emit(MissionEventKind.Info, "Mission start requested · arming");
        }

        public void Abort()
        {
            if (phase == DemoPhase.Arming)
            {
                SetPhase(DemoPhase.Idle);
                Emit(MissionEventKind.Critical, "ABORT · arming cancelled");
                return;
            }

            if (phase != DemoPhase.Takeoff && phase != DemoPhase.Search && phase != DemoPhase.Exit)
                return;

            Emit(MissionEventKind.Critical, "ABORT · returning to launch pad over mapped area");

            var home = RescuePathPlanner.Build(scene).Plan(pos, scene.LaunchPad);
            if (home == null)
            {
                Emit(MissionEventKind.Warning, "No known route home · landing in place");
                BeginLanding(forDock: false);
                return;
            }

            route = ToWaypoints(home);
            waypointIndex = 0;
            holdUntil = 0;
            SetPhase(DemoPhase.Returning);
        }

        private static List<Waypoint> ToWaypoints(List<PointF> points)
        {
            var wps = new List<Waypoint>(points.Count);
            foreach (var p in points)
                wps.Add(new Waypoint(p.X, p.Y));
            return wps;
        }

        public DemoSnapshot Sample()
        {
            var now = clock();
            var dt = (float)Math.Max(0, Math.Min(0.25, now - lastSample));
            lastSample = now;

            Step(now, dt);

            var climb = dt > 0 ? (altitude - lastAltitude) / dt : 0;
            lastAltitude = altitude;
            var speed = Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);

            DrainBattery(dt, climb);
            DriftLinkStats();

            // Attitude from the horizontal velocity change: nose down to accelerate, bank into turns.
            var fwd = new PointF((float)Math.Sin(yawDeg * Math.PI / 180), (float)Math.Cos(yawDeg * Math.PI / 180));
            var along = velocity.X * fwd.X + velocity.Y * fwd.Y;
            var across = velocity.X * fwd.Y - velocity.Y * fwd.X;

            return new DemoSnapshot
            {
                Phase = phase,
                Mode = ModeFor(phase),
                Armed = phase != DemoPhase.Idle && phase != DemoPhase.Complete && phase != DemoPhase.Charging,
                BatteryPercent = (int)Math.Round(battery),
                BatteryVolts = PackVolts(),
                CurrentAmps = CurrentFor(climb),
                BatteryTempC = tempC,
                AltitudeM = altitude,
                ClimbRate = climb,
                GroundSpeed = speed,
                RollDeg = IsFlying ? across * 9.0 + Noise(0.4) : Noise(0.1),
                PitchDeg = IsFlying ? -along * 7.5 + Noise(0.4) : Noise(0.1),
                YawDeg = (yawDeg + 360) % 360,
                MissionTime = takeoffAt >= 0 ? TimeSpan.FromSeconds(now - takeoffAt) : (TimeSpan?)null,
                LinkQuality = linkQuality,
                BytesPerSecond = (int)bandwidth,
                LatencyMs = latencyMs
            };
        }

        private void Step(double now, float dt)
        {
            var tp = (float)(now - phaseStart);

            switch (phase)
            {
                case DemoPhase.Arming:
                    if (tp >= ArmSeconds)
                    {
                        SetPhase(DemoPhase.Takeoff);
                        takeoffAt = now;
                        Emit(MissionEventKind.Info, "Armed · taking off from launch pad");
                    }
                    break;

                case DemoPhase.Takeoff:
                    altitude = CruiseAltitude * SmoothStep(Math.Min(1f, tp / TakeoffSeconds));
                    if (tp >= TakeoffSeconds)
                    {
                        if (resumingFromDock)
                        {
                            resumingFromDock = false;
                            route = savedRoute;
                            waypointIndex = savedWaypointIndex;
                            savedRoute = null;
                            Emit(MissionEventKind.Info, "Recharged · resuming search from where it left off");
                        }
                        else
                        {
                            route = SearchRoute();
                            waypointIndex = 0;
                            Emit(MissionEventKind.Info, "Search started · GPS-denied, navigating on LiDAR/optical-flow SLAM");
                        }
                        SetPhase(DemoPhase.Search);
                    }
                    break;

                case DemoPhase.Search:
                case DemoPhase.Exit:
                case DemoPhase.Returning:
                case DemoPhase.DockingReturn:
                    // A dock trigger replaces the route and phase for this tick - run the
                    // rest of the search-leg logic next tick against the new route instead.
                    if (phase == DemoPhase.Search && CheckDockingTrigger())
                        break;

                    FollowRoute(now, dt);
                    // Eased rather than set: an abort mid-takeoff must keep climbing, not jump to cruise.
                    var hold = CruiseAltitude + 0.04f * (float)Math.Sin(now * 1.3) + (float)Noise(0.01);
                    altitude += (hold - altitude) * Math.Min(1f, dt * 1.5f);
                    break;

                case DemoPhase.Landing:
                    altitude = landFromAltitude * (1f - SmoothStep(Math.Min(1f, tp / LandSeconds)));
                    velocity = PointF.Empty;
                    if (tp >= LandSeconds)
                    {
                        altitude = 0;
                        if (landingForDock)
                        {
                            landingForDock = false;
                            SetPhase(DemoPhase.Charging);
                            Emit(MissionEventKind.Info, "At dock · autonomous charging in progress");
                        }
                        else
                        {
                            SetPhase(DemoPhase.Complete);
                            Emit(MissionEventKind.Info, "Landed · disarmed · " + scene.Survivors.Count +
                                                        " survivors reported");
                        }
                    }
                    break;

                case DemoPhase.Charging:
                    velocity = PointF.Empty;
                    battery = Math.Min(100, battery + dt * ChargeRatePercentPerSecond);
                    if (battery >= ResumeBatteryPercent)
                    {
                        resumingFromDock = true;
                        SetPhase(DemoPhase.Takeoff);
                        Emit(MissionEventKind.Info, "Charged to " + (int)battery + "% · resuming mission");
                    }
                    break;
            }

            UpdateDroneInScene();

            if (IsFlying)
            {
                Reveal();
                if (scene.Trail.Count == 0 || Distance(scene.Trail[scene.Trail.Count - 1], pos) > 0.15f)
                    scene.Trail.Add(new Point3(pos.X, pos.Y, altitude));
            }
        }

        private void FollowRoute(double now, float dt)
        {
            if (route == null || waypointIndex >= route.Count)
            {
                RouteFinished();
                return;
            }

            if (now < holdUntil)
            {
                // Hovering on an inspection point: ease to a stop and face what was found.
                velocity = new PointF(velocity.X * 0.85f, velocity.Y * 0.85f);
                var s = NearestSurvivor();
                if (s.HasValue)
                    TurnTowards(s.Value, dt);
                return;
            }

            var speed = phase == DemoPhase.Returning || phase == DemoPhase.DockingReturn ? ReturnSpeed : CruiseSpeed;
            var target = route[waypointIndex].P;
            var dx = target.X - pos.X;
            var dy = target.Y - pos.Y;
            var dist = (float)Math.Sqrt(dx * dx + dy * dy);
            var step = speed * dt;

            if (dist <= Math.Max(step, 0.02f))
            {
                pos = target;
                if (route[waypointIndex].HoldSeconds > 0)
                    holdUntil = now + route[waypointIndex].HoldSeconds;
                waypointIndex++;
                return;
            }

            // Ease the velocity toward the commanded one so corners are rounded, not instant.
            var want = new PointF(dx / dist * speed, dy / dist * speed);
            var k = Math.Min(1f, dt * 3.5f);
            velocity = new PointF(velocity.X + (want.X - velocity.X) * k, velocity.Y + (want.Y - velocity.Y) * k);
            pos = new PointF(pos.X + velocity.X * dt, pos.Y + velocity.Y * dt);
            TurnTowards(target, dt);
        }

        private void RouteFinished()
        {
            switch (phase)
            {
                case DemoPhase.Search:
                    route = ExitRoute();
                    waypointIndex = 0;
                    SetPhase(DemoPhase.Exit);
                    Emit(MissionEventKind.Info, "Search complete · " + (int)(scene.Explored.Fraction * 100) +
                                                "% explored · heading for exit");
                    break;

                case DemoPhase.Exit:
                    Emit(MissionEventKind.Info, "Clear of the arena exit · landing");
                    BeginLanding(forDock: false);
                    break;

                case DemoPhase.Returning:
                    Emit(MissionEventKind.Info, "Over launch pad · landing");
                    BeginLanding(forDock: false);
                    break;

                case DemoPhase.DockingReturn:
                    Emit(MissionEventKind.Info, "Over the dock · landing to recharge");
                    BeginLanding(forDock: true);
                    break;
            }
        }

        private void BeginLanding(bool forDock)
        {
            landFromAltitude = altitude;
            landingForDock = forDock;
            velocity = PointF.Empty;
            SetPhase(DemoPhase.Landing);
        }

        /// <summary>
        /// Once per run, breaks off a search leg for the "Mission Continuity & Auto
        /// Charging" cycle: save the interrupted route, plan a route home over the known
        /// map (same planner the rescue routes use), and head for the dock. Returns true
        /// if it triggered this tick.
        /// </summary>
        private bool CheckDockingTrigger()
        {
            if (dockCycleDone || battery > DockTriggerBatteryPercent)
                return false;

            dockCycleDone = true;
            savedRoute = route;
            savedWaypointIndex = waypointIndex;

            var home = RescuePathPlanner.Build(scene).Plan(pos, scene.LaunchPad);
            route = home != null ? ToWaypoints(home) : new List<Waypoint> { new Waypoint(scene.LaunchPad.X, scene.LaunchPad.Y) };
            waypointIndex = 0;
            holdUntil = 0;
            SetPhase(DemoPhase.DockingReturn);
            Emit(MissionEventKind.Warning, "Battery at " + (int)battery + "% · autonomous return to dock for recharge");
            return true;
        }

        private void TurnTowards(PointF target, float dt)
        {
            var want = (float)(Math.Atan2(target.X - pos.X, target.Y - pos.Y) * 180 / Math.PI);
            var diff = ((want - yawDeg) % 360 + 540) % 360 - 180;
            var maxStep = 110f * dt;
            yawDeg += Math.Max(-maxStep, Math.Min(maxStep, diff));
        }

        private PointF? NearestSurvivor()
        {
            PointF? best = null;
            var bestD = float.MaxValue;
            foreach (var s in scene.Survivors)
            {
                var d = Distance(s.Position, pos);
                if (d < bestD && d < 3f)
                {
                    bestD = d;
                    best = s.Position;
                }
            }
            return best;
        }

        #region sensing

        /// <summary>Marks everything in sensor range and line of sight as seen, and reports new detections.</summary>
        private void Reveal()
        {
            var changed = false;
            var res = ExploredMap.Resolution;
            var r = (int)Math.Ceiling(SensorRange / res);
            int cx0 = (int)(pos.X / res), cy0 = (int)(pos.Y / res);

            for (var cy = cy0 - r; cy <= cy0 + r; cy++)
            {
                for (var cx = cx0 - r; cx <= cx0 + r; cx++)
                {
                    if (scene.Explored[cx, cy] || cx < 0 || cy < 0 || cx >= scene.Explored.Size || cy >= scene.Explored.Size)
                        continue;

                    var c = new PointF((cx + 0.5f) * res, (cy + 0.5f) * res);
                    if (Distance(c, pos) <= SensorRange && MissionScene.LineOfSight(pos, c, truthWalls))
                        changed |= scene.Explored.Mark(cx, cy);
                }
            }

            if (RevealWalls())
                changed = true;

            RescuePathPlanner survivorPlanner = null;

            for (var i = 0; i < truthSurvivors.Count; i++)
            {
                var p = truthSurvivors[i];
                if (scene.Survivors.Exists(s => s.Position == p))
                    continue;
                if (Distance(p, pos) > SurvivorRange || !MissionScene.LineOfSight(pos, p, truthWalls))
                    continue;

                var d = new Detection
                {
                    Id = scene.Survivors.Count + 1,
                    Kind = DetectionKind.Survivor,
                    Position = p,
                    DetectedAt = MissionClock()
                };
                scene.Survivors.Add(d);
                changed = true;
                Emit(MissionEventKind.Survivor, "Survivor " + d.Name + " located · grid " + d.Grid);

                // Plan and highlight its rescue route the instant it's found, so the "best
                // path" is visible live during the flight without the operator clicking
                // anything - built lazily since most ticks find nothing new.
                survivorPlanner = survivorPlanner ?? RescuePathPlanner.Build(scene);
                d.Route = survivorPlanner.Plan(scene.Entry, d.Position);
                scene.SelectedSurvivorId = d.Id;
            }

            for (var i = 0; i < truthHazards.Count; i++)
            {
                var truth = truthHazards[i];
                if (scene.Hazards.Exists(h => h.Position == truth.Position))
                    continue;
                if (Distance(truth.Position, pos) > HazardRange || !MissionScene.LineOfSight(pos, truth.Position, truthWalls))
                    continue;

                var h2 = new Detection
                {
                    Id = scene.Hazards.Count + 1,
                    Kind = DetectionKind.Hazard,
                    Type = truth.Type,
                    Priority = truth.Priority,
                    Position = truth.Position,
                    DetectedAt = MissionClock(),
                    HazardRadius = 1.4f
                };
                scene.Hazards.Add(h2);
                changed = true;
                Emit(MissionEventKind.Hazard, HazardVisuals.Label(h2.Type) + " hazard detected · " +
                                              h2.Priority.ToString().ToUpperInvariant() + " priority · grid " +
                                              h2.Grid + " · rescue routes re-planned");
            }

            if (changed)
                MapVersion++;
        }

        /// <summary>
        /// Walls are revealed in short pieces (each piece visible from the drone), then merged
        /// back into runs so the scene - and the 3D view - holds a handful of segments, not hundreds.
        /// </summary>
        private bool RevealWalls()
        {
            var any = false;

            for (var w = 0; w < truthWalls.Count; w++)
            {
                var wall = truthWalls[w];
                var pieces = revealedPieces[w];
                for (var i = 0; i < pieces.Length; i++)
                {
                    if (pieces[i])
                        continue;

                    var t = (i + 0.5f) / pieces.Length;
                    var mid = new PointF(wall.A.X + (wall.B.X - wall.A.X) * t, wall.A.Y + (wall.B.Y - wall.A.Y) * t);
                    var d = Distance(mid, pos);
                    if (d > SensorRange)
                        continue;

                    // Aim just short of the wall so the wall itself does not block the ray.
                    var probe = new PointF(mid.X + (pos.X - mid.X) * 0.05f / Math.Max(d, 0.05f),
                                           mid.Y + (pos.Y - mid.Y) * 0.05f / Math.Max(d, 0.05f));
                    if (MissionScene.LineOfSight(pos, probe, truthWalls))
                    {
                        pieces[i] = true;
                        any = true;
                    }
                }
            }

            if (any)
                RebuildSceneWalls();
            return any;
        }

        private void RebuildSceneWalls()
        {
            scene.Walls.Clear();
            for (var w = 0; w < truthWalls.Count; w++)
            {
                var wall = truthWalls[w];
                var pieces = revealedPieces[w];
                var runStart = -1;
                for (var i = 0; i <= pieces.Length; i++)
                {
                    var on = i < pieces.Length && pieces[i];
                    if (on && runStart < 0)
                        runStart = i;
                    else if (!on && runStart >= 0)
                    {
                        float t0 = runStart / (float)pieces.Length, t1 = i / (float)pieces.Length;
                        scene.Walls.Add(new Wall(
                            wall.A.X + (wall.B.X - wall.A.X) * t0, wall.A.Y + (wall.B.Y - wall.A.Y) * t0,
                            wall.A.X + (wall.B.X - wall.A.X) * t1, wall.A.Y + (wall.B.Y - wall.A.Y) * t1));
                        runStart = -1;
                    }
                }
            }
        }

        #endregion

        #region arena

        /// <summary>
        /// A 15 m arena of rooms off a central corridor, entry bottom-left, exit top-right.
        /// Doors are all at least 1.5 m (the brief's minimum corridor is 1 m).
        /// </summary>
        private void BuildArena()
        {
            float[,] walls =
            {
                // perimeter, with the entry (x 1-3) and exit (x 12-14) gaps
                { 0, 0, 1, 0 }, { 3, 0, 15, 0 }, { 15, 0, 15, 15 },
                { 0, 15, 12, 15 }, { 14, 15, 15, 15 }, { 0, 0, 0, 15 },

                { 4, 0, 4, 3 }, { 4, 4.5f, 4, 9 },                      // entry hall | room 2
                { 10, 0, 10, 5 }, { 10, 6.5f, 10, 9 },                  // room 2 | room 3
                { 4, 9, 6, 9 }, { 7.5f, 9, 12, 9 }, { 13.5f, 9, 15, 9 },// rooms | corridor
                { 0, 11, 1.5f, 11 }, { 3, 11, 9, 11 },                  // corridor | room 4
                { 9, 11, 9, 12.5f }, { 9, 14, 9, 15 },                  // room 4 | room 5
                { 11, 11, 15, 11 },                                     // corridor | room 5
                { 7, 0, 7, 4 },                                         // bay divider, room 2
                { 12.5f, 3, 15, 3 },                                    // alcove, room 3
                { 4.5f, 13, 4.5f, 15 }                                  // partition, room 4
            };

            for (var i = 0; i < walls.GetLength(0); i++)
            {
                var w = new Wall(walls[i, 0], walls[i, 1], walls[i, 2], walls[i, 3]);
                truthWalls.Add(w);
                revealedPieces.Add(new bool[Math.Max(1, (int)Math.Round(w.Length / WallPiece))]);
            }

            // Not a fixed head-count - real sites hold however many people are actually
            // there. This is simply how many the scripted demo route happens to pass.
            truthSurvivors.AddRange(new[]
            {
                new PointF(5.5f, 1.4f), new PointF(8.6f, 1.3f), new PointF(13.8f, 1.5f),
                new PointF(13.6f, 13.4f), new PointF(6.6f, 14.1f)
            });

            // A spread of the onboard model's hazard classes, each with a triaged priority.
            truthHazards.Add((new PointF(14.2f, 7.4f), HazardType.Fire, HazardPriority.Critical));
            truthHazards.Add((new PointF(7.4f, 10.0f), HazardType.Electrical, HazardPriority.High));
            truthHazards.Add((new PointF(4.2f, 13.6f), HazardType.Flood, HazardPriority.Medium));
            truthHazards.Add((new PointF(12.0f, 4.6f), HazardType.Structural, HazardPriority.Low));
        }

        /// <summary>Room-by-room search, holding briefly wherever a survivor comes into view.</summary>
        private static List<Waypoint> SearchRoute()
        {
            const float look = 2.5f;
            return new List<Waypoint>
            {
                new Waypoint(2.0f, 3.75f), new Waypoint(4.0f, 3.75f), new Waypoint(5.5f, 3.6f),
                new Waypoint(5.5f, 2.6f, look),
                new Waypoint(5.5f, 5.6f), new Waypoint(8.5f, 5.6f),
                new Waypoint(8.5f, 2.4f, look),
                new Waypoint(8.5f, 5.75f), new Waypoint(10.0f, 5.75f), new Waypoint(11.3f, 5.75f, 1.5f),
                new Waypoint(11.4f, 4.2f), new Waypoint(11.4f, 1.7f),
                new Waypoint(12.6f, 1.7f, look),
                new Waypoint(11.4f, 1.9f), new Waypoint(11.4f, 4.2f), new Waypoint(11.7f, 7.2f),
                new Waypoint(12.75f, 8.3f), new Waypoint(12.75f, 10.0f),
                new Waypoint(10.0f, 10.0f, 1.5f),
                new Waypoint(10.0f, 12.0f),
                new Waypoint(12.4f, 13.0f, look),
                new Waypoint(10.5f, 13.25f), new Waypoint(9.0f, 13.25f),
                new Waypoint(7.6f, 13.0f, look),
                new Waypoint(5.5f, 12.2f), new Waypoint(3.2f, 12.2f),
                new Waypoint(2.3f, 13.0f, look)
            };
        }

        private static List<Waypoint> ExitRoute()
        {
            return new List<Waypoint>
            {
                new Waypoint(3.2f, 12.2f), new Waypoint(5.5f, 12.2f), new Waypoint(7.6f, 12.4f),
                new Waypoint(9.0f, 13.25f), new Waypoint(11.2f, 13.9f), new Waypoint(13.0f, 14.3f),
                new Waypoint(13.0f, 16.0f)
            };
        }

        #endregion

        #region vehicle state

        private void SetPhase(DemoPhase next)
        {
            phase = next;
            phaseStart = clock();
        }

        private void UpdateDroneInScene()
        {
            scene.HasDrone = true;
            scene.Drone = new Point3(pos.X, pos.Y, altitude);
            scene.DroneYawDeg = yawDeg;
            scene.DroneArmed = phase != DemoPhase.Idle && phase != DemoPhase.Complete && phase != DemoPhase.Charging;
        }

        private TimeSpan MissionClock()
        {
            return takeoffAt >= 0 ? TimeSpan.FromSeconds(clock() - takeoffAt) : TimeSpan.Zero;
        }

        private void Emit(MissionEventKind kind, string text)
        {
            pendingEvents.Add(new MissionEvent(MissionClock(), kind, text));
        }

        private static string ModeFor(DemoPhase p)
        {
            switch (p)
            {
                case DemoPhase.Landing: return "LAND";
                case DemoPhase.Returning:
                case DemoPhase.DockingReturn: return "RTL";
                case DemoPhase.Charging: return "HOLD";
                case DemoPhase.Idle:
                case DemoPhase.Complete: return "STABILIZE";
                default: return "GUIDED";
            }
        }

        private double CurrentFor(double climb)
        {
            if (!IsFlying)
                return phase == DemoPhase.Arming ? 1.6 : 0.4;
            return 19.0 + Math.Max(0, climb) * 6.0 + Noise(0.6);
        }

        private void DrainBattery(float dt, double climb)
        {
            if (IsFlying)
            {
                // Compressed so a three-minute demo shows a visible drain; about 9 %/min.
                battery = Math.Max(0, battery - dt * 0.15 * (climb > 0.1 ? 1.4 : 1.0));
                tempC = Math.Min(48, tempC + dt * 0.05);
            }
            else
            {
                tempC = Math.Max(27, tempC - dt * 0.02);
            }

            if (battery <= 20 && (phase == DemoPhase.Search || phase == DemoPhase.Exit))
            {
                Emit(MissionEventKind.Warning, "Low battery failsafe at " + (int)battery + "%");
                Abort();
            }
        }

        /// <summary>4S pack: resting cell voltage from charge, minus sag under load.</summary>
        private double PackVolts()
        {
            var cell = 3.45 + 0.75 * Math.Pow(battery / 100.0, 0.8);
            return 4 * cell - (IsFlying ? 0.35 : 0) + Noise(0.01);
        }

        private void DriftLinkStats()
        {
            linkQuality = Clamp(linkQuality + Noise(0.012) - (linkQuality - 0.975) * 0.08, 0.84, 1.0);
            if (rng.NextDouble() < 0.006)
                linkQuality = Clamp(linkQuality - 0.09, 0.72, 1.0);

            latencyMs = Clamp(latencyMs + Noise(2.2) - (latencyMs - 26) * 0.12, 14, 90);
            if (rng.NextDouble() < 0.01)
                latencyMs = Clamp(latencyMs + 35, 14, 120);

            var target = IsFlying ? 5200 : 3600;
            bandwidth = Clamp(bandwidth + Noise(90) - (bandwidth - target) * 0.1, 1800, 9000);
        }

        #endregion

        private double Noise(double amplitude)
        {
            return (rng.NextDouble() - 0.5) * 2.0 * amplitude;
        }

        private static double Clamp(double v, double min, double max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        private static float Distance(PointF a, PointF b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static float Distance(Point3 a, PointF b)
        {
            return Distance(new PointF(a.X, a.Y), b);
        }

        private static float SmoothStep(float x)
        {
            return x * x * (3f - 2f * x);
        }
    }
}
