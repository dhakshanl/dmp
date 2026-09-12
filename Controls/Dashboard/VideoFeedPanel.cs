#if !LIB
// GStreamer hands us a MissionPlanner.Drawing bitmap (Skia-backed), not a GDI+ one, so the
// same extern-alias dance Controls\GimbalVideoControl.cs uses is needed to name both types.
extern alias Drawing;
using MPBitmap = Drawing::System.Drawing.Bitmap;
#else
using MPBitmap = System.Drawing.Bitmap;
#endif

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using log4net;
using MissionPlanner.Utilities;
using SkiaSharp;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// A single self-contained live video tile: owns one GStreamer pipeline, paints the
    /// most recent frame letterboxed to fit, and falls back to a "NO SIGNAL" card when the
    /// link is down. Used twice on the Mission page - once for the ZED depth view and once
    /// for the RGB left-eye view.
    ///
    /// Deliberately a Control rather than a PictureBox: ThemeManager repaints stock
    /// controls with the active theme, and this tile is always dark (see the note in
    /// GCSViews\Mission.Designer.cs). Painting it ourselves also lets the caption and the
    /// no-signal state share one buffer instead of stacking overlay labels on top.
    /// </summary>
    public class VideoFeedPanel : Control
    {
        private static readonly ILog log =
            LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static class NativeMethods
        {
            [DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory")]
            internal static extern void CopyMemory(IntPtr Destination, IntPtr Source, int Length);
        }

        private readonly object frameLock = new object();
        private Bitmap front;   // painted by OnPaint
        private Bitmap back;    // written by the GStreamer thread, then swapped with front
        private GStreamer stream;
        private string pipeline;
        private bool wantRunning;
        private DateTime lastFrameUtc = DateTime.MinValue;
        private readonly Timer watchdog;

        // Loop bookkeeping. `generation` is bumped by every Start/Stop so a restart queued
        // by a pipeline that has since been torn down (demo mode switched off mid-file) is
        // dropped instead of resurrecting the stream.
        private int generation;
        private int emptyRuns;
        private bool sawFrameThisRun;
        private bool restartingForLoop;

        /// <summary>Label drawn in the tile's top-left corner. Also names it while offline.</summary>
        public string Caption { get; set; } = "";

        /// <summary>Corner text size; the small picture-in-picture tile wants a smaller one.</summary>
        public float CaptionSize { get; set; } = 9f;

        /// <summary>Seconds without a frame before the tile reverts to the no-signal card.</summary>
        public double SignalTimeoutSeconds { get; set; } = 3.0;

        /// <summary>
        /// Restart the pipeline when it ends, for finite sources (a file). A live UDP
        /// stream never reaches EOS, so this is only meaningful for the demo playback in
        /// GCSViews\Mission.cs. Restarts stop after MaxEmptyRuns consecutive runs that
        /// produced no frames at all, so a bad path cannot spin forever.
        /// </summary>
        public bool Loop { get; set; }

        private const int MaxEmptyRuns = 3;

        public bool HasSignal
        {
            get { return (DateTime.UtcNow - lastFrameUtc).TotalSeconds < SignalTimeoutSeconds; }
        }

        public VideoFeedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(10, 10, 16);

            // One tick drives both the repaint and the no-signal transition. The GStreamer
            // thread only swaps in the new frame; it never touches the UI directly, so a
            // stalled or spamming pipeline cannot outpace the paint loop.
            watchdog = new Timer { Interval = 66 };
            watchdog.Tick += (s, e) => Invalidate();
        }

        /// <summary>
        /// Starts (or restarts) the pipeline. The string must end in an appsink named
        /// "outsink" emitting video/x-raw,format=BGRA - that is what ExtLibs\Utilities\
        /// GStreamer.cs looks up and how it interprets the buffer.
        /// </summary>
        public void Start(string gstPipeline)
        {
            Stop();

            if (string.IsNullOrWhiteSpace(gstPipeline))
                return;

            pipeline = gstPipeline;
            wantRunning = true;
            sawFrameThisRun = false;
            generation++;

            // A fresh Start is an explicit request, so it clears the loop failure budget;
            // a loop restart deliberately keeps counting.
            if (!restartingForLoop)
                emptyRuns = 0;

            if (!GStreamer.GstLaunchExists)
                GStreamer.GstLaunch = GStreamer.LookForGstreamer();

            if (!GStreamer.GstLaunchExists)
            {
                log.Info("VideoFeedPanel: no GStreamer runtime found, tile stays offline");
                wantRunning = false;
                Invalidate();
                return;
            }

            try
            {
                stream = new GStreamer();
                stream.OnNewImage += OnNewImage;
                stream.Start(pipeline);
                watchdog.Start();
            }
            catch (Exception ex)
            {
                log.Error("VideoFeedPanel: failed to start " + pipeline, ex);
                wantRunning = false;
            }

            Invalidate();
        }

        public void Stop()
        {
            wantRunning = false;
            generation++;
            watchdog.Stop();

            if (stream != null)
            {
                stream.OnNewImage -= OnNewImage;
                try
                {
                    stream.Stop();
                }
                catch (Exception ex)
                {
                    log.Error("VideoFeedPanel: error stopping pipeline", ex);
                }
                stream = null;
            }

            lastFrameUtc = DateTime.MinValue;
            Invalidate();
        }

        /// <summary>
        /// Called on the GStreamer worker thread as it finishes. The restart itself must be
        /// marshalled to the UI thread: Start -> Stop -> GStreamer.Stop() joins the worker,
        /// which would deadlock if it ran on the worker itself.
        /// </summary>
        private void QueueLoopRestart()
        {
            if (!wantRunning)
                return;

            if (sawFrameThisRun)
                emptyRuns = 0;
            else if (++emptyRuns >= MaxEmptyRuns)
            {
                log.Info("VideoFeedPanel: giving up looping, " + MaxEmptyRuns +
                         " runs produced no frames: " + pipeline);
                return;
            }

            var expected = generation;

            try
            {
                if (IsDisposed || !IsHandleCreated)
                    return;

                BeginInvoke((Action)(() =>
                {
                    // Stopped or restarted while this hop was queued - let that win.
                    if (generation != expected || !wantRunning || IsDisposed)
                        return;

                    var again = pipeline;
                    restartingForLoop = true;
                    try
                    {
                        Start(again);
                    }
                    finally
                    {
                        restartingForLoop = false;
                    }
                }));
            }
            catch (Exception ex)
            {
                // handle torn down between the guard and the marshalled call
                log.Debug("VideoFeedPanel: loop restart not queued", ex);
            }
        }

        /// <summary>
        /// Pipeline for playing a local video file, decoded to what OnNewImage expects.
        /// Paths are passed with forward slashes and quoted: gst_parse_launch treats a
        /// backslash as an escape, so a raw Windows path silently mangles the location.
        /// </summary>
        public static string FilePipeline(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var gstPath = path.Replace("\\", "/");

            return "filesrc location=\"" + gstPath + "\" ! decodebin ! videoconvert " +
                   "! video/x-raw,format=BGRA ! appsink name=outsink sync=true";
        }

        /// <summary>
        /// Runs on the GStreamer worker thread. The frame MUST be copied here and not
        /// marshalled to the UI thread as-is: the bitmap only wraps the mapped native
        /// buffer, and GStreamer.cs unmaps and unrefs it the moment this returns, so a
        /// deferred read would land on freed memory.
        /// </summary>
        private void OnNewImage(object sender, MPBitmap image)
        {
            if (image == null)
            {
                // GStreamer signals pipeline exit with a null frame.
                lastFrameUtc = DateTime.MinValue;
                if (Loop)
                    QueueLoopRestart();
                return;
            }

            sawFrameThisRun = true;

            try
            {
                var w = image.Width;
                var h = image.Height;
                if (w <= 0 || h <= 0)
                    return;

                var src = image.LockBits(Rectangle.Empty, null, SKColorType.Bgra8888).Scan0;
                if (src == IntPtr.Zero)
                    return;

                lock (frameLock)
                {
                    // Reuse the back buffer across frames - at 1080p a fresh allocation per
                    // frame is ~8 MB of churn 30 times a second.
                    if (back == null || back.Width != w || back.Height != h)
                    {
                        back?.Dispose();
                        back = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                    }

                    var bd = back.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly,
                        PixelFormat.Format32bppPArgb);
                    try
                    {
                        var srcStride = 4 * w;
                        if (bd.Stride == srcStride)
                        {
                            NativeMethods.CopyMemory(bd.Scan0, src, srcStride * h);
                        }
                        else
                        {
                            for (var y = 0; y < h; y++)
                                NativeMethods.CopyMemory(bd.Scan0 + y * bd.Stride,
                                    src + y * srcStride, srcStride);
                        }
                    }
                    finally
                    {
                        back.UnlockBits(bd);
                    }

                    var swap = front;
                    front = back;
                    back = swap;
                }

                lastFrameUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                log.Error("VideoFeedPanel: error copying frame", ex);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            if (HasSignal)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                lock (frameLock)
                {
                    if (front != null)
                        g.DrawImage(front, FitRect(front.Width, front.Height));
                }
            }
            else
            {
                DrawNoSignal(g);
            }

            DrawCaption(g);
        }

        /// <summary>Largest rect of the frame's aspect ratio that fits, centred (letterboxed).</summary>
        private Rectangle FitRect(int imgW, int imgH)
        {
            if (imgW <= 0 || imgH <= 0 || Width <= 0 || Height <= 0)
                return new Rectangle(0, 0, Width, Height);

            var scale = Math.Min(Width / (double)imgW, Height / (double)imgH);
            var w = (int)Math.Round(imgW * scale);
            var h = (int)Math.Round(imgH * scale);
            return new Rectangle((Width - w) / 2, (Height - h) / 2, w, h);
        }

        private void DrawNoSignal(Graphics g)
        {
            var msg = wantRunning ? "CONNECTING..." : "NO SIGNAL";
            using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(110, 110, 140)))
            using (var fmt = new StringFormat
                   { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(msg, font, brush, new RectangleF(0, 0, Width, Height), fmt);
            }

            using (var pen = new Pen(Color.FromArgb(45, 45, 66), 1f))
            {
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        private void DrawCaption(Graphics g)
        {
            if (string.IsNullOrEmpty(Caption))
                return;

            using (var font = new Font("Segoe UI", CaptionSize, FontStyle.Bold))
            {
                var size = g.MeasureString(Caption, font);
                var box = new RectangleF(6, 6, size.Width + 10, size.Height + 4);
                using (var bg = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                    g.FillRectangle(bg, box);
                using (var fg = new SolidBrush(HasSignal
                           ? Color.FromArgb(225, 230, 245)
                           : Color.FromArgb(130, 130, 160)))
                    g.DrawString(Caption, font, fg, box.X + 5, box.Y + 2);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Stop();
                watchdog.Dispose();
                lock (frameLock)
                {
                    front?.Dispose();
                    back?.Dispose();
                    front = back = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
