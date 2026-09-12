using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Windows.Forms;
using MissionPlanner.Controls;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// "Connection Health" dashboard card. Reuses the same Rx observable composition
    /// pattern as Controls.ConnectionStats (link quality / bandwidth / max packet
    /// interval over MAVLinkInterface.WhenPacketReceived / WhenPacketLost / BytesReceived)
    /// but renders into this dashboard's dark card styling instead of ConnectionStats'
    /// plain default-styled textboxes. CombineWithDefault/CalculateAverage are duplicated
    /// (not reused) because they're private to ConnectionStats; ToHumanReadableByteCount
    /// is public there and is called directly.
    /// </summary>
    public class ConnectionHealthPanel : UserControl
    {
        private readonly Label _titleLabel;
        private readonly Label _linkQualityLabel;
        private readonly Label _linkQualityValue;
        private readonly Label _bandwidthLabel;
        private readonly Label _bandwidthValue;
        private readonly Label _latencyLabel;
        private readonly Label _latencyValue;
        private readonly DashboardSparkline _latencySpark;

        private MAVLinkInterface _mavlink;
        private CompositeDisposable _subscriptions;

        public ConnectionHealthPanel()
        {
            BackColor = Color.FromArgb(24, 24, 38);

            _titleLabel = MakeLabel("Connection Health", 10.5f, FontStyle.Bold, Color.White);
            _linkQualityLabel = MakeLabel("Link Quality", 9f, FontStyle.Regular, Color.Silver);
            _linkQualityValue = MakeLabel("--", 9f, FontStyle.Bold, Color.Cyan);
            _bandwidthLabel = MakeLabel("Bandwidth usage", 9f, FontStyle.Regular, Color.Silver);
            _bandwidthValue = MakeLabel("--", 9f, FontStyle.Bold, Color.Cyan);
            _latencyLabel = MakeLabel("Latency (ms)", 9f, FontStyle.Regular, Color.Silver);
            _latencyValue = MakeLabel("--", 9f, FontStyle.Bold, Color.Orange);

            _latencySpark = new DashboardSparkline(Color.Orange) { Dock = DockStyle.Fill };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            layout.Controls.Add(_titleLabel, 0, 0);
            layout.SetColumnSpan(_titleLabel, 2);
            layout.Controls.Add(_linkQualityLabel, 0, 1);
            layout.Controls.Add(_linkQualityValue, 1, 1);
            layout.Controls.Add(_bandwidthLabel, 0, 2);
            layout.Controls.Add(_bandwidthValue, 1, 2);
            layout.Controls.Add(_latencyLabel, 0, 3);
            layout.Controls.Add(_latencyValue, 1, 3);
            layout.Controls.Add(_latencySpark, 0, 4);
            layout.SetColumnSpan(_latencySpark, 2);

            Controls.Add(layout);
        }

        private static Label MakeLabel(string text, float size, FontStyle style, Color color)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", size, style),
                ForeColor = color,
                BackColor = Color.Transparent,
                Margin = new Padding(4, 4, 4, 4),
                // see CreateSmallLabel in GCSViews\Mission.Designer.cs — "custom" opts this
                // label out of ThemeManager's ForeColor override
                Tag = "custom"
            };
        }

        public void Attach(MAVLinkInterface mavlink)
        {
            Detach();
            _mavlink = mavlink;
            if (_mavlink == null)
                return;

            _subscriptions = new CompositeDisposable();

            var linkQuality = CombineWithDefault(_mavlink.WhenPacketReceived, _mavlink.WhenPacketLost, Tuple.Create)
                .Buffer(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1))
                .Select(CalculateAverage);
            _subscriptions.Add(linkQuality.Subscribe(x =>
                OnUiThread(() => _linkQualityValue.Text = x.ToString("00%"))));

            var bytesPerSecond = _mavlink.BytesReceived
                .Buffer(TimeSpan.FromSeconds(1))
                .Select(bytes => bytes.Sum())
                .Buffer(3, 1)
                .Select(xs => xs.Any() ? (int)xs.Average() : 0)
                .Select(ConnectionStats.ToHumanReadableByteCount);
            _subscriptions.Add(bytesPerSecond.Subscribe(x =>
                OnUiThread(() => _bandwidthValue.Text = x + "/s")));

            // Inter-packet gap, sampled once a second, as a latency proxy. Deliberately NOT
            // ConnectionStats' running Scan(Math.Max) — a monotonic maximum only ever climbs,
            // which makes a useless (never-recovering) trend line.
            var intervalMs = _mavlink.WhenPacketReceived
                .TimeInterval()
                .Select(x => x.Interval.TotalMilliseconds)
                .Sample(TimeSpan.FromSeconds(1));
            _subscriptions.Add(intervalMs.Subscribe(ms => OnUiThread(() =>
            {
                _latencyValue.Text = ms.ToString("0") + " ms";
                _latencySpark.PushValue(ms);
            })));
        }

        /// <summary>
        /// Marshals to the UI thread via the control itself rather than ObserveOn
        /// (SynchronizationContext.Current), which would be null — and throw — if Attach ever
        /// runs off the UI thread. MainSwitcher.ShowScreen calls IActivate.Activate() without
        /// marshalling, so that is a reachable path.
        /// </summary>
        private void OnUiThread(Action action)
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

        /// <summary>
        /// Drives the card from simulated numbers for the Mission page's demo mode. Call
        /// Detach() first: this deliberately does not tear down a live subscription, so
        /// pushing demo values over a still-attached link would fight the real feed.
        /// </summary>
        public void ShowDemoSample(double linkQuality, int bytesPerSecond, double latencyMs)
        {
            OnUiThread(() =>
            {
                _linkQualityValue.Text = linkQuality.ToString("00%");
                _bandwidthValue.Text = ConnectionStats.ToHumanReadableByteCount(bytesPerSecond) + "/s";
                _latencyValue.Text = latencyMs.ToString("0") + " ms";
                _latencySpark.PushValue(latencyMs);
            });
        }

        public void Detach()
        {
            _subscriptions?.Dispose();
            _subscriptions = null;

            _linkQualityValue.Text = "--";
            _bandwidthValue.Text = "--";
            _latencyValue.Text = "--";
        }

        private static IObservable<TResult> CombineWithDefault<TSource, TResult>(IObservable<TSource> first,
            Subject<TSource> second, Func<TSource, TSource, TResult> resultSelector)
        {
            return Observable.Defer(() =>
            {
                var merged = new Subject<TResult>();
                first.Select(x => resultSelector(x, default(TSource))).Subscribe(merged);
                second.Select(x => resultSelector(default(TSource), x)).Subscribe(merged);
                return merged;
            });
        }

        private static double CalculateAverage(IList<Tuple<int, int>> xs)
        {
            var packetsReceived = xs.Sum(t => t.Item1);
            var packetsLost = xs.Sum(t => t.Item2);
            if (packetsReceived + packetsLost == 0)
                return 0;
            return packetsReceived / (packetsReceived + (double)packetsLost);
        }
    }
}
