using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// Date-folder-grouped ULog file browser for the Mission page's telemetry drawer. Files
    /// are grouped by parent folder (Mission Planner writes each session's logs into its own
    /// date-stamped folder), most recent folder first.
    ///
    /// Keep this a subclass: Utilities\ThemeManager.ApplyTheme matches
    /// `ctl.GetType() == typeof(TreeView)` exactly and would otherwise repaint this with the
    /// app theme's background instead of the drawer's dark palette.
    /// </summary>
    public class MissionLogBrowser : TreeView
    {
        public event Action<string> FileSelected;

        public MissionLogBrowser()
        {
            HideSelection = false;
            AfterSelect += MissionLogBrowser_AfterSelect;
        }

        private void MissionLogBrowser_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is string path)
                FileSelected?.Invoke(path);
        }

        private static List<string> GetFiles(string path, string pattern)
        {
            var files = new List<string>();
            var directories = new string[] { };

            try
            {
                files.AddRange(Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly));
                directories = Directory.GetDirectories(path);
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }

            foreach (var directory in directories)
            {
                try
                {
                    files.AddRange(GetFiles(directory, pattern));
                }
                catch (UnauthorizedAccessException) { }
            }

            return files;
        }

        /// <summary>Rescans rootPath for *.ulg files and rebuilds the tree.</summary>
        public void RefreshLogs(string rootPath)
        {
            BeginUpdate();
            Nodes.Clear();

            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                EndUpdate();
                return;
            }

            var files = GetFiles(rootPath, "*.ulg").Select(f => new FileInfo(f));

            var groups = files
                .GroupBy(f => f.DirectoryName ?? rootPath)
                .OrderByDescending(g => g.Key);

            foreach (var group in groups)
            {
                var folderName = new DirectoryInfo(group.Key).Name;
                var folderNode = new TreeNode(folderName);

                foreach (var file in group.OrderByDescending(f => f.LastWriteTime))
                {
                    var leaf = new TreeNode($"{file.Name}   {ToHumanReadableSize(file.Length)}")
                    {
                        Tag = file.FullName
                    };
                    folderNode.Nodes.Add(leaf);
                }

                Nodes.Add(folderNode);
            }

            ExpandAll();
            EndUpdate();
        }

        /// <summary>
        /// Prefix on a demo node's Tag. Real nodes carry a plain filesystem path, so
        /// GCSViews\Mission.cs can tell a fabricated log from one it should actually parse.
        /// </summary>
        public const string DemoTagPrefix = "demo://";

        /// <summary>
        /// Fills the tree with a plausible set of recorded sorties for the Mission page's
        /// demo mode - a GCS with no flights on disk yet still shows a populated log
        /// manager. Dates are relative to now, so the tree never looks stale.
        /// </summary>
        public void ShowDemoLogs()
        {
            // Fixed seed: the same demo run twice should list the same flights.
            var rng = new Random(4127);
            var today = DateTime.Now.Date;

            BeginUpdate();
            Nodes.Clear();

            for (var dayOffset = 0; dayOffset < 3; dayOffset++)
            {
                var day = today.AddDays(-dayOffset);
                var folderNode = new TreeNode(day.ToString("yyyy-MM-dd") + " sortie");

                var count = 3 - dayOffset;
                for (var i = 0; i < count; i++)
                {
                    var stamp = day.AddHours(9 + i * 2).AddMinutes(rng.Next(0, 59));
                    var name = "log_" + stamp.ToString("yyyy-MM-dd_HH-mm-ss") + ".ulg";
                    var size = (long)(rng.Next(24, 180) * 1024L * 1024L);

                    folderNode.Nodes.Add(new TreeNode($"{name}   {ToHumanReadableSize(size)}")
                    {
                        Tag = DemoTagPrefix + name
                    });
                }

                Nodes.Add(folderNode);
            }

            ExpandAll();
            EndUpdate();
        }

        private static string ToHumanReadableSize(long bytes)
        {
            if (bytes > 1024 * 1024) return $"{bytes / (1024f * 1024f):0.0} MB";
            if (bytes > 1024) return $"{bytes / 1024f:0.0} KB";
            return $"{bytes} B";
        }
    }
}
