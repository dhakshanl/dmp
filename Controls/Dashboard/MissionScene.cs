using System;
using System.Collections.Generic;
using System.Drawing;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// Anchors the local arena frame (MissionScene's metres, X east/Y north) onto a real
    /// GPS coordinate, so a local (x, y) can be converted to lat/lon the moment GPS
    /// becomes available - the "Geo-Tagging" GPS-enabled-mode capability the SIH26177
    /// architecture lists alongside GPS-denied mode's local grid boxes.
    ///
    /// A flat-earth (equirectangular) approximation - accurate to well under a centimetre
    /// across a site this size, nowhere near GPS's own few-metre error budget.
    /// </summary>
    public struct GeoOrigin
    {
        private const double MetresPerDegLat = 111320.0;

        public double Lat;
        public double Lon;

        /// <summary>Compass heading, degrees, that the local +Y axis actually points at.
        /// 0 (the default) means local north is already true north - fine until a real
        /// site survey supplies the building's actual orientation.</summary>
        public float HeadingDeg;

        public GeoOrigin(double lat, double lon, float headingDeg = 0f)
        {
            Lat = lat;
            Lon = lon;
            HeadingDeg = headingDeg;
        }

        public (double Lat, double Lon) ToLatLon(PointF local)
        {
            var rad = HeadingDeg * Math.PI / 180.0;
            var east = local.X * Math.Cos(rad) + local.Y * Math.Sin(rad);
            var north = -local.X * Math.Sin(rad) + local.Y * Math.Cos(rad);

            var metresPerDegLon = MetresPerDegLat * Math.Cos(Lat * Math.PI / 180.0);
            return (Lat + north / MetresPerDegLat, Lon + east / metresPerDegLon);
        }
    }

    /// <summary>A mapped wall, as a centreline segment in arena metres.</summary>
    public struct Wall
    {
        public PointF A;
        public PointF B;

        public Wall(float x1, float y1, float x2, float y2)
        {
            A = new PointF(x1, y1);
            B = new PointF(x2, y2);
        }

        public float Length
        {
            get { return (float)Math.Sqrt((B.X - A.X) * (B.X - A.X) + (B.Y - A.Y) * (B.Y - A.Y)); }
        }
    }

    public struct Point3
    {
        public float X, Y, Z;

        public Point3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public enum DetectionKind
    {
        Survivor,
        Hazard
    }

    /// <summary>
    /// The onboard AI's multi-class hazard output (unified RGB + thermal classification,
    /// per the SIH26177 architecture) rather than a single fire/no-fire flag.
    /// </summary>
    public enum HazardType
    {
        Fire,
        Flood,
        Electrical,
        Structural
    }

    /// <summary>Risk/priority the onboard model assigns a hazard, for triage on approach.</summary>
    public enum HazardPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>Display colour, label and marker shape for each hazard class, kept next to the model it describes.</summary>
    public static class HazardVisuals
    {
        public static Color Color(HazardType type)
        {
            switch (type)
            {
                case HazardType.Flood: return System.Drawing.Color.FromArgb(80, 170, 255);
                case HazardType.Electrical: return System.Drawing.Color.FromArgb(255, 214, 64);
                case HazardType.Structural: return System.Drawing.Color.FromArgb(255, 150, 90);
                default: return System.Drawing.Color.FromArgb(255, 92, 58); // Fire
            }
        }

        public static string Label(HazardType type)
        {
            switch (type)
            {
                case HazardType.Flood: return "Flood";
                case HazardType.Electrical: return "Electrical";
                case HazardType.Structural: return "Structural";
                default: return "Fire";
            }
        }

        /// <summary>Short code used in a hazard's pin name, e.g. "F1", "W2", "E3", "C4".</summary>
        public static string Code(HazardType type)
        {
            switch (type)
            {
                case HazardType.Flood: return "W";
                case HazardType.Electrical: return "E";
                case HazardType.Structural: return "C";
                default: return "F";
            }
        }

        public static Color PriorityColor(HazardPriority p)
        {
            switch (p)
            {
                case HazardPriority.Critical: return MissionPalette.Danger;
                case HazardPriority.High: return MissionPalette.Warning;
                case HazardPriority.Medium: return System.Drawing.Color.FromArgb(230, 200, 90);
                default: return MissionPalette.TextDim;
            }
        }
    }

    /// <summary>Something the onboard sensing has found and reported a position for.</summary>
    public class Detection
    {
        public int Id;
        public DetectionKind Kind;
        public PointF Position;

        /// <summary>Hazard only: which class the onboard model assigned it.</summary>
        public HazardType Type;

        /// <summary>Hazard only: the risk/priority score from onboard triage.</summary>
        public HazardPriority Priority;

        /// <summary>Mission time at which it was first reported.</summary>
        public TimeSpan DetectedAt;

        /// <summary>Hazard only: keep-out radius the rescue route is planned around, metres.</summary>
        public float HazardRadius = 1.4f;

        /// <summary>Survivor only: safe route from the entry, null until one has been found.</summary>
        public List<PointF> Route;

        public float RouteLength
        {
            get
            {
                if (Route == null || Route.Count < 2)
                    return -1;

                float d = 0;
                for (var i = 1; i < Route.Count; i++)
                {
                    var dx = Route[i].X - Route[i - 1].X;
                    var dy = Route[i].Y - Route[i - 1].Y;
                    d += (float)Math.Sqrt(dx * dx + dy * dy);
                }
                return d;
            }
        }

        public string Grid
        {
            get { return MissionGrid.Label(Position); }
        }

        public string Name
        {
            get { return (Kind == DetectionKind.Survivor ? "S" : HazardVisuals.Code(Type)) + Id; }
        }
    }

    /// <summary>
    /// The arena's grid-box naming, used as the geo-tag for every detection when no GPS
    /// fix is available: columns A.. west to east, rows 1.. south (entry side) to north.
    /// A GPS-enabled flight would geo-tag with lat/lon instead - see MissionStatus.GpsFix
    /// in MissionStatusBar.cs for which mode the vehicle is actually in.
    /// </summary>
    public static class MissionGrid
    {
        public const float CellMetres = 1.0f;

        public static string Label(PointF p)
        {
            var col = (int)Math.Floor(p.X / CellMetres);
            var row = (int)Math.Floor(p.Y / CellMetres);
            col = Math.Max(0, Math.Min(25, col));
            row = Math.Max(0, row);
            return ((char)('A' + col)).ToString() + (row + 1);
        }

        public static string ColumnLabel(int col)
        {
            return ((char)('A' + Math.Max(0, Math.Min(25, col)))).ToString();
        }
    }

    /// <summary>
    /// Which parts of the arena floor the drone has actually seen, at half-metre
    /// resolution. Drives both the "explored" shading in the 3D view and the rescue
    /// planner, which only routes over ground that has been seen.
    /// </summary>
    public class ExploredMap
    {
        public const float Resolution = 0.5f;

        private readonly bool[,] cells;
        private int count;

        public int Size { get; private set; }

        public ExploredMap(float arenaSize)
        {
            Size = (int)Math.Ceiling(arenaSize / Resolution);
            cells = new bool[Size, Size];
        }

        public bool this[int cx, int cy]
        {
            get { return cx >= 0 && cy >= 0 && cx < Size && cy < Size && cells[cx, cy]; }
        }

        public bool IsExplored(float x, float y)
        {
            return this[(int)Math.Floor(x / Resolution), (int)Math.Floor(y / Resolution)];
        }

        /// <summary>Returns true if the cell was newly revealed.</summary>
        public bool Mark(int cx, int cy)
        {
            if (cx < 0 || cy < 0 || cx >= Size || cy >= Size || cells[cx, cy])
                return false;

            cells[cx, cy] = true;
            count++;
            return true;
        }

        public float Fraction
        {
            get { return Size == 0 ? 0 : count / (float)(Size * Size); }
        }

        public void Clear()
        {
            Array.Clear(cells, 0, cells.Length);
            count = 0;
        }
    }

    /// <summary>
    /// Everything the Mission page's 3D view draws, in arena metres: X east, Y north (away
    /// from the entry), Z up. Filled by the demo simulator today; the companion computer's
    /// map feed will fill the same model once that link exists.
    /// </summary>
    public class MissionScene
    {
        // No fixed survivor cap: a real disaster site can hold any number, unlike a
        // competition arena with a declared maximum. Counts are shown open-ended.

        /// <summary>Footprint the 3D view renders, in metres. A stand-in for whatever
        /// extent the deployed site's LiDAR/SLAM map actually covers.</summary>
        public float ArenaSize = 15f;

        /// <summary>Centre of the opening in the south wall the drone enters through.</summary>
        public PointF Entry = new PointF(2f, 0f);

        /// <summary>Centre of the opening in the north wall the mission ends at.</summary>
        public PointF Exit = new PointF(13f, 15f);

        /// <summary>
        /// Launch point, and - per the SIH26177 "mission continuity" architecture - also
        /// the autonomous docking/charging station the drone returns to on low battery
        /// and resumes search from once recharged.
        /// </summary>
        public PointF LaunchPad = new PointF(2f, 0.9f);

        public readonly List<Wall> Walls = new List<Wall>();
        public readonly List<Detection> Survivors = new List<Detection>();
        public readonly List<Detection> Hazards = new List<Detection>();
        public readonly List<Point3> Trail = new List<Point3>();
        public ExploredMap Explored;

        public bool HasDrone;
        public Point3 Drone;
        public float DroneYawDeg;
        public bool DroneArmed;

        /// <summary>Survivor whose rescue route is highlighted; 0 for none.</summary>
        public int SelectedSurvivorId;

        /// <summary>
        /// Anchors this scene's local metres onto the real world; null until something has
        /// supplied one (the demo simulator's fabricated site coordinate, or the live
        /// vehicle's home position once it has a fix). Without it, GPS-enabled mode has
        /// nothing to geo-tag against and the page stays on local grid boxes.
        /// </summary>
        public GeoOrigin? GpsOrigin;

        /// <summary>
        /// True while there's a GPS fix good enough to navigate and geo-tag with - drives
        /// the page's "Automatic Switching" between the world map (GPS-enabled) and the
        /// local 3D view (GPS-denied), matching the SIH26177 architecture's two modes.
        /// </summary>
        public bool GpsAvailable;

        /// <summary>
        /// Where RTL brings the drone back to, in arena metres: LaunchPad (the dock) in the
        /// demo, the local-frame origin (the vehicle's home) for a live flight.
        /// </summary>
        public PointF RtlHome;

        /// <summary>
        /// Shortest known path from the drone back to RtlHome, kept current while airborne
        /// so the world map can always show where an RTL would take it; null on the ground.
        /// </summary>
        public List<PointF> RtlRoute;

        /// <summary>True while the vehicle is actually flying that return leg (RTL / return to dock).</summary>
        public bool RtlActive;

        /// <summary>
        /// Outdoor (GPS-enabled) area survey only: the planned lawnmower pattern as its lane
        /// end points in flying order, so the world map can draw the pattern ahead of the
        /// drone. Null for the indoor room-by-room search, which has no such pattern.
        /// </summary>
        public List<PointF> SurveyPattern;

        public MissionScene()
        {
            Explored = new ExploredMap(ArenaSize);
        }

        /// <summary>Switches to a site of a different footprint; everything explored so far is dropped.</summary>
        public void Resize(float arenaSize)
        {
            ArenaSize = arenaSize;
            Explored = new ExploredMap(arenaSize);
        }

        /// <summary>
        /// How a detection at this position should be labelled right now: a real lat/lon
        /// once GPS is available and anchored, otherwise the local grid box GPS-denied
        /// mode reports instead. One call site for both list rows and event-log text, so
        /// they can never disagree about which mode the vehicle is actually in.
        /// </summary>
        public string FormatTag(PointF position)
        {
            if (GpsAvailable && GpsOrigin.HasValue)
            {
                var (lat, lon) = GpsOrigin.Value.ToLatLon(position);
                return "GPS " + lat.ToString("0.00000") + ", " + lon.ToString("0.00000");
            }

            return "Grid " + MissionGrid.Label(position);
        }

        public Detection SelectedSurvivor
        {
            get { return Survivors.Find(s => s.Id == SelectedSurvivorId); }
        }

        public void Clear()
        {
            Walls.Clear();
            Survivors.Clear();
            Hazards.Clear();
            Trail.Clear();
            Explored.Clear();
            HasDrone = false;
            DroneArmed = false;
            SelectedSurvivorId = 0;
            RtlRoute = null;
            RtlActive = false;
        }

        /// <summary>True if nothing in <paramref name="walls"/> crosses the segment a-b.</summary>
        public static bool LineOfSight(PointF a, PointF b, List<Wall> walls)
        {
            for (var i = 0; i < walls.Count; i++)
            {
                if (SegmentsIntersect(a, b, walls[i].A, walls[i].B))
                    return false;
            }
            return true;
        }

        public static bool SegmentsIntersect(PointF p1, PointF p2, PointF q1, PointF q2)
        {
            var d1 = Cross(q1, q2, p1);
            var d2 = Cross(q1, q2, p2);
            var d3 = Cross(p1, p2, q1);
            var d4 = Cross(p1, p2, q2);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                   ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        private static float Cross(PointF a, PointF b, PointF c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }

        /// <summary>Shortest distance from p to the segment a-b.</summary>
        public static float DistanceToSegment(PointF p, PointF a, PointF b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len2 = dx * dx + dy * dy;
            var t = len2 <= 1e-6f ? 0f : ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
            t = Math.Max(0f, Math.Min(1f, t));
            var cx = a.X + t * dx - p.X;
            var cy = a.Y + t * dy - p.Y;
            return (float)Math.Sqrt(cx * cx + cy * cy);
        }
    }
}
