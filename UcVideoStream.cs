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
using SharpDX;

namespace MissionPlanner
{
    public partial class UcVideoStream : UserControl
    {
        public UcVideoStream()
        {
            InitializeComponent();
            StartButton.Enabled = true;
            StopButton.Enabled = false;
        }

        VideoCapture cap;
        Timer timer;

        void SetStartButtonEnabled(bool enabled)
        {
            if (StartButton.InvokeRequired)
                StartButton.Invoke(new Action(() => StartButton.Enabled = enabled));
            else
                StartButton.Enabled = enabled;
        }

        void SetStopButtonEnabled(bool enabled)
        {
            if (StopButton.InvokeRequired)
                StopButton.Invoke(new Action(() => StopButton.Enabled = enabled));
            else
                StopButton.Enabled = enabled;
        }

        public async Task StartRTSP(string url)
        {
            StopStream(); // Clean up previous state
            SetStartButtonEnabled(false); // Disable start
            SetStopButtonEnabled(false);  // Initially disabled while connecting

            //Show loading image
            VideoBox.Image = Properties.Resources.LoadingImage;

            // Try to create VideoCapture with a timeout
            var openTask = Task.Run(() =>
            {
                try { return new VideoCapture(url); }
                catch { return null; }
            });

            if (await Task.WhenAny(openTask, Task.Delay(15000)) == openTask) // 15 sec timeout
            {
                cap = openTask.Result;
                if (cap == null || !cap.IsOpened())
                {
                    MessageBox.Show("Error: Could not open video stream. \nCheck URL, network connection, and dependencies (FFmpeg).", "Stream Error");
                    cap?.Dispose();
                    cap = null;
                    SetStartButtonEnabled(true);
                    SetStopButtonEnabled(false);
                    VideoBox.Image = Properties.Resources.no_video;
                    return;
                }

                timer = new Timer { Interval = 33 };
                timer.Tick += (s, e) =>
                {
                    try
                    {
                        using (Mat frame = new Mat())
                        {
                            if (cap != null && cap.IsOpened() && cap.Read(frame))
                            {
                                VideoBox.Image?.Dispose();
                                VideoBox.Image = frame.ToBitmap();
                            }
                        }
                    }
                    catch
                    {
                        SetStartButtonEnabled(true);
                        SetStopButtonEnabled(false);
                        timer.Stop();
                    }
                };
                timer.Start();
                SetStopButtonEnabled(true);  // Enable stop only after stream starts
            }
            else
            {
                VideoBox.Image = Properties.Resources.no_video;
                MessageBox.Show("Stream connection timed out after 15 seconds.", "Timeout");
                cap?.Dispose();
                cap = null;
                SetStartButtonEnabled(true);
                SetStopButtonEnabled(false);
            }
        }

        public void StopStream()
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
            VideoBox.Image = Properties.Resources.no_video;
            SetStartButtonEnabled(true);    // Allow restart
            SetStopButtonEnabled(false);    // Prevent multiple stops
        }

        public async void start_camera_Click(object sender, EventArgs e)
        {
            string url = "rtsp://192.168.144.25:8554/main.264";
            var result = InputBox.Show("RTSP URL", "Enter camera URL", ref url);

            if (result == DialogResult.Cancel)
            {
                cap = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Please enter a valid URL to start.", "URL Invalid");
                SetStopButtonEnabled(false);
                return;
            }

            await StartRTSP(url);
        }

        public void stop_camera_Click(object sender, EventArgs e)
        {
            StopStream();
        }

        public Button StartButton => start_camera;
        public Button StopButton => stop_camera;
    }

}
