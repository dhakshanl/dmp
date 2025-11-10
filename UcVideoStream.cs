using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner.Controls;
using OpenCvSharp.Extensions;

namespace MissionPlanner
{
    public partial class UcVideoStream : UserControl
    {
        public UcVideoStream()
        {
            InitializeComponent();
        }

        VideoCapture cap;
        Timer timer;

        private void StartRTSP(String url)
        {
            /*cap = new VideoCapture(url);
             timer = new Timer { Interval = 33 }; // ~30 FPS
             timer.Tick += (s, e) =>
             {
                 using (Mat frame = new Mat())
                 {
                     if (cap.Read(frame))
                     {
                         VideoBox.Image?.Dispose();
                         VideoBox.Image = BitmapConverter.ToBitmap(frame);
                     }
                 }
             };
             timer.Start();
            */
            try
            {
                cap = new VideoCapture(url);
            }
            catch (Exception ex)
            {
                // Log the exception (ex.ToString())
                MessageBox.Show($"Failed to initialize VideoCapture: {ex.Message}", "Init Error");
                return;
            }

            // !!! IMPORTANT CHECK !!!
            // Check if cap is null OR if the stream isn't opened
            if (cap == null || !cap.IsOpened())
            {
                MessageBox.Show("Error: Could not open video stream. \nCheck URL, network connection, and dependencies (FFmpeg).", "Stream Error");
                cap?.Dispose(); // Clean up if it was created but not opened
                return;
            }

            // Only set up and start the timer if the capture is valid
            timer = new Timer { Interval = 33 }; // ~30 FPS
            timer.Tick += (s, e) =>
            {
                try
                {
                    using (Mat frame = new Mat())
                    {
                        // The Read() method returns false if no frame is grabbed.
                        // This is the only check you need inside the loop.
                        if (cap != null && cap.IsOpened() && cap.Read(frame))
                        {
                            // If Read() returned true, the frame is valid.
                            VideoBox.Image = frame.ToBitmap();
                        }
                    }
                }
                catch (Exception tickEx)
                {
                    timer.Stop();
                    MessageBox.Show($"Error during video processing: {tickEx.Message}", "Runtime Error");
                }
            };
            timer.Start();
        }
        private void VideoBox_Click(object sender, EventArgs e)
        {

        }

        private void start_camera_Click(object sender, EventArgs e)
        {
            string url = "rtsp://192.168.0.23:8554/live";
            InputBox.Show("rtsp url", "Enter the url to the rtsp source url", ref url);

            if (!string.IsNullOrWhiteSpace(url))
                StartRTSP(url);
        }

        private void stop_camera_Click(object sender, EventArgs e)
        {
            timer?.Stop();
            timer = null;

            if (cap != null)
            {
                cap.Release();
                cap.Dispose();
                cap = null;
            }

            VideoBox.Image?.Dispose();
            VideoBox.Image = null;
        }
    }
}
