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
            SetStartButtonEnabled(true);
            SetStopButtonEnabled(false);
        }

        VideoCapture cap;
        Timer timer;
        private bool isStreaming = false;
        private Control videoParent = null; // Remembers VideoBox parent for fullscreen

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
            SetStartButtonEnabled(false); // Disable start during connect
            SetStopButtonEnabled(false);  // Disable stop until streaming

            VideoBox.Image = Properties.Resources.LoadingImage;

            var openTask = Task.Run(() =>
            {
                try { return new VideoCapture(url); }
                catch { return null; }
            });

            if (await Task.WhenAny(openTask, Task.Delay(30000)) == openTask) // 30 sec timeout
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
                    isStreaming = false;
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
                        isStreaming = false;
                    }
                };
                timer.Start();
                SetStopButtonEnabled(true);   // Allow user to stop while streaming
                isStreaming = true;
            }
            else
            {
                VideoBox.Image = Properties.Resources.no_video;
                MessageBox.Show("Stream connection timed out after 30 seconds.", "Timeout");
                cap?.Dispose();
                cap = null;
                SetStartButtonEnabled(true);
                SetStopButtonEnabled(false);
                isStreaming = false;
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

            isStreaming = false;

            VideoBox.Image?.Dispose();
            VideoBox.Image = Properties.Resources.no_video;
            SetStartButtonEnabled(true);
            SetStopButtonEnabled(false);
        }

        public async void start_camera_Click(object sender, EventArgs e)
        {
            if (isStreaming)
            {
                MessageBox.Show("Video stream is already running.");
                return;
            }

            string url = "rtsp://192.168.144.25:8554/main.264";
            var result = InputBox.Show("RTSP URL", "Enter camera URL", ref url);

            if (result == DialogResult.Cancel)
            {
                cap = null;
                isStreaming = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Please enter a valid URL to start.", "URL Invalid");
                SetStopButtonEnabled(false);
                isStreaming = false;
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

       
        private void full_Screen_Button_Click(object sender, EventArgs e)
        {
            // Don't allow full screen if no stream/image
            if (VideoBox.Image == null) return;

            Form fullScreenForm = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                WindowState = FormWindowState.Maximized,
                TopMost = true,
                BackColor = Color.Black
            };

            // Remember parent, remove from UI, add to full screen form
            videoParent = VideoBox.Parent;
            var videoBoxIndex = videoParent.Controls.GetChildIndex(VideoBox);
            videoParent.Controls.Remove(VideoBox);
            VideoBox.Dock = DockStyle.Fill;
            fullScreenForm.Controls.Add(VideoBox);

            // Closing actions
            fullScreenForm.KeyDown += (s, ea) =>
            {
                if (ea.KeyCode == Keys.Escape)
                    fullScreenForm.Close();
            };
            VideoBox.Click += (s, ea) => fullScreenForm.Close();

            fullScreenForm.FormClosing += (s, ea) =>
            {
                fullScreenForm.Controls.Remove(VideoBox);
                VideoBox.Dock = DockStyle.None;
                videoParent.Controls.Add(VideoBox);
                videoParent.Controls.SetChildIndex(VideoBox, videoBoxIndex);
            };

            fullScreenForm.ShowDialog();
        }
        public Button fullScreenButton => full_Screen_Button;
    }
}
