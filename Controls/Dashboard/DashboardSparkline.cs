using System;
using System.Drawing;
using ZedGraph;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// A bare, axis-less, legend-less ZedGraphControl configured as a small inline trend
    /// line ("sparkline"). Reuses the AddCurve + rolling-window-on-tick pattern already
    /// established in GCSViews.FlightData.CreateChart/ZedGraphTimer_Tick and
    /// Controls.RAW_Sensor, just reduced to a single curve with all chrome hidden.
    ///
    /// Keep this a subclass: Utilities\ThemeManager.ApplyTheme matches
    /// `ctl.GetType() == typeof(ZedGraphControl)` exactly and would otherwise overwrite the
    /// pane fills and force Line.Width = 2, undoing the sparkline styling on screen switch.
    /// </summary>
    public class DashboardSparkline : ZedGraphControl
    {
        private readonly RollingPointPairList _points;
        private readonly LineItem _curve;
        private double _time;
        private readonly double _windowSeconds;

        public DashboardSparkline(Color lineColor, int capacity = 120, double windowSeconds = 60)
        {
            _windowSeconds = windowSeconds;
            _points = new RollingPointPairList(capacity);

            IsShowPointValues = false;
            IsAntiAlias = true;
            IsShowHScrollBar = false;
            IsShowVScrollBar = false;
            IsEnableSelection = false;
            IsEnableZoom = false;
            IsEnableHPan = false;
            IsEnableVPan = false;

            var pane = GraphPane;
            pane.Title.IsVisible = false;
            pane.Legend.IsVisible = false;
            pane.Chart.Border.IsVisible = false;
            pane.Border.IsVisible = false;
            pane.Margin.All = 0;

            pane.XAxis.IsVisible = false;
            pane.X2Axis.IsVisible = false;
            pane.YAxis.IsVisible = false;
            pane.Y2Axis.IsVisible = false;

            pane.Fill = new Fill(Color.Transparent);
            pane.Chart.Fill = new Fill(Color.FromArgb(34, 34, 50));

            _curve = pane.AddCurve(string.Empty, _points, lineColor, SymbolType.None);
            _curve.Line.Width = 1.6f;
            _curve.Line.IsAntiAlias = true;

            pane.XAxis.Scale.Min = 0;
            pane.XAxis.Scale.Max = _windowSeconds;
        }

        /// <summary>Appends a new sample at the current time and redraws.</summary>
        public void PushValue(double value)
        {
            _time += 1.0;
            _points.Add(_time, value);

            var xScale = GraphPane.XAxis.Scale;
            if (_time > xScale.Max)
            {
                xScale.Max = _time;
                xScale.Min = _time - _windowSeconds;
            }

            try
            {
                AxisChange();
            }
            catch
            {
                // matches the existing swallow-and-redraw pattern in Controls.RAW_Sensor.timer1_Tick
            }

            Invalidate();
        }
    }
}
