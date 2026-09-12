using System;
using System.Collections.Generic;
using System.Drawing;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// Plans the "best path" the Mission page highlights: the safe walking route a rescue
    /// team would take from the arena entry to a survivor. It routes only over floor the
    /// drone has actually seen, keeps a body-width off every mapped wall, and gives every
    /// known fire its keep-out radius - so the route is one the map can vouch for, not a
    /// guess through unexplored space.
    ///
    /// Grid A* (8-connected, no corner cutting) followed by line-of-sight string pulling,
    /// which turns the staircase grid path into the few straight legs a person would walk.
    /// A 15 m arena at 0.25 m is 3600 cells, so a plan is well under a millisecond; the
    /// grid is built once per map update and reused for every survivor.
    /// </summary>
    public class RescuePathPlanner
    {
        private const float Cell = 0.25f;

        /// <summary>Half a shoulder width plus margin, metres.</summary>
        private const float WallClearance = 0.4f;

        /// <summary>How far a blocked start/goal may be nudged to the nearest free cell.</summary>
        private const int SnapRadiusCells = 5;

        private readonly int n;
        private readonly bool[] blocked;

        private RescuePathPlanner(int size)
        {
            n = size;
            blocked = new bool[n * n];
        }

        public static RescuePathPlanner Build(MissionScene scene)
        {
            var planner = new RescuePathPlanner((int)Math.Ceiling(scene.ArenaSize / Cell));

            for (var cy = 0; cy < planner.n; cy++)
            {
                for (var cx = 0; cx < planner.n; cx++)
                {
                    var p = new PointF((cx + 0.5f) * Cell, (cy + 0.5f) * Cell);
                    planner.blocked[cy * planner.n + cx] = !IsWalkable(scene, p);
                }
            }

            return planner;
        }

        private static bool IsWalkable(MissionScene scene, PointF p)
        {
            if (!scene.Explored.IsExplored(p.X, p.Y))
                return false;

            foreach (var w in scene.Walls)
            {
                if (MissionScene.DistanceToSegment(p, w.A, w.B) < WallClearance)
                    return false;
            }

            foreach (var h in scene.Hazards)
            {
                var dx = p.X - h.Position.X;
                var dy = p.Y - h.Position.Y;
                if (dx * dx + dy * dy < h.HazardRadius * h.HazardRadius)
                    return false;
            }

            return true;
        }

        /// <summary>Straight-leg route from <paramref name="from"/> to <paramref name="to"/>, or null if the known map has none.</summary>
        public List<PointF> Plan(PointF from, PointF to)
        {
            var start = Snap(ToCell(from));
            var goal = Snap(ToCell(to));
            if (start < 0 || goal < 0)
                return null;

            var cells = AStar(start, goal);
            if (cells == null)
                return null;

            var route = StringPull(cells);

            // End on the survivor itself, not the free cell beside them the search stopped at.
            route[0] = from;
            route.Add(to);
            return route;
        }

        private int ToCell(PointF p)
        {
            var cx = Math.Max(0, Math.Min(n - 1, (int)Math.Floor(p.X / Cell)));
            var cy = Math.Max(0, Math.Min(n - 1, (int)Math.Floor(p.Y / Cell)));
            return cy * n + cx;
        }

        private PointF CellCentre(int index)
        {
            return new PointF((index % n + 0.5f) * Cell, (index / n + 0.5f) * Cell);
        }

        /// <summary>Nearest free cell to <paramref name="index"/> within the snap radius; -1 if none.</summary>
        private int Snap(int index)
        {
            if (!blocked[index])
                return index;

            int cx0 = index % n, cy0 = index / n;
            var best = -1;
            var bestD = int.MaxValue;

            for (var dy = -SnapRadiusCells; dy <= SnapRadiusCells; dy++)
            {
                for (var dx = -SnapRadiusCells; dx <= SnapRadiusCells; dx++)
                {
                    int cx = cx0 + dx, cy = cy0 + dy;
                    if (cx < 0 || cy < 0 || cx >= n || cy >= n || blocked[cy * n + cx])
                        continue;

                    var d = dx * dx + dy * dy;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = cy * n + cx;
                    }
                }
            }

            return best;
        }

        private static readonly int[] StepX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] StepY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        private const float Diagonal = 1.41421356f;

        private List<int> AStar(int start, int goal)
        {
            var g = new float[n * n];
            var parent = new int[n * n];
            var closed = new bool[n * n];
            for (var i = 0; i < g.Length; i++)
            {
                g[i] = float.MaxValue;
                parent[i] = -1;
            }

            var open = new MinHeap();
            g[start] = 0;
            open.Push(start, Heuristic(start, goal));

            while (open.Count > 0)
            {
                var cur = open.Pop();
                if (closed[cur])
                    continue;
                if (cur == goal)
                    return Unwind(parent, goal);

                closed[cur] = true;
                int cx = cur % n, cy = cur / n;

                for (var k = 0; k < 8; k++)
                {
                    int nx = cx + StepX[k], ny = cy + StepY[k];
                    if (nx < 0 || ny < 0 || nx >= n || ny >= n)
                        continue;

                    var next = ny * n + nx;
                    if (blocked[next] || closed[next])
                        continue;

                    // No squeezing diagonally between two blocked cells (a wall corner).
                    if (k >= 4 && (blocked[cy * n + nx] || blocked[ny * n + cx]))
                        continue;

                    var cost = g[cur] + (k >= 4 ? Diagonal : 1f);
                    if (cost < g[next])
                    {
                        g[next] = cost;
                        parent[next] = cur;
                        open.Push(next, cost + Heuristic(next, goal));
                    }
                }
            }

            return null;
        }

        /// <summary>Octile distance - exact for an 8-connected grid with no obstacles.</summary>
        private float Heuristic(int a, int b)
        {
            var dx = Math.Abs(a % n - b % n);
            var dy = Math.Abs(a / n - b / n);
            return Math.Max(dx, dy) + (Diagonal - 1f) * Math.Min(dx, dy);
        }

        private static List<int> Unwind(int[] parent, int goal)
        {
            var path = new List<int>();
            for (var c = goal; c >= 0; c = parent[c])
                path.Add(c);
            path.Reverse();
            return path;
        }

        /// <summary>Greedily replaces runs of grid cells with the longest straight leg that stays clear.</summary>
        private List<PointF> StringPull(List<int> cells)
        {
            var route = new List<PointF> { CellCentre(cells[0]) };
            var anchor = 0;

            while (anchor < cells.Count - 1)
            {
                var reach = anchor + 1;
                for (var j = cells.Count - 1; j > anchor + 1; j--)
                {
                    if (Clear(CellCentre(cells[anchor]), CellCentre(cells[j])))
                    {
                        reach = j;
                        break;
                    }
                }

                route.Add(CellCentre(cells[reach]));
                anchor = reach;
            }

            return route;
        }

        private bool Clear(PointF a, PointF b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var steps = (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / (Cell * 0.5f));

            for (var i = 1; i < steps; i++)
            {
                var t = i / (float)steps;
                if (blocked[ToCell(new PointF(a.X + dx * t, a.Y + dy * t))])
                    return false;
            }
            return true;
        }

        /// <summary>Binary min-heap of cell indices keyed by f-cost; stale entries are skipped via the closed set.</summary>
        private class MinHeap
        {
            private readonly List<KeyValuePair<float, int>> items = new List<KeyValuePair<float, int>>();

            public int Count
            {
                get { return items.Count; }
            }

            public void Push(int value, float key)
            {
                items.Add(new KeyValuePair<float, int>(key, value));
                var i = items.Count - 1;
                while (i > 0)
                {
                    var p = (i - 1) / 2;
                    if (items[p].Key <= items[i].Key)
                        break;
                    Swap(i, p);
                    i = p;
                }
            }

            public int Pop()
            {
                var top = items[0].Value;
                var last = items.Count - 1;
                items[0] = items[last];
                items.RemoveAt(last);

                var i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < items.Count && items[l].Key < items[m].Key) m = l;
                    if (r < items.Count && items[r].Key < items[m].Key) m = r;
                    if (m == i)
                        break;
                    Swap(i, m);
                    i = m;
                }

                return top;
            }

            private void Swap(int a, int b)
            {
                var t = items[a];
                items[a] = items[b];
                items[b] = t;
            }
        }
    }
}
