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
            cap = new VideoCapture(url);
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
        }
        private void VideoBox_Click(object sender, EventArgs e)
        {

        }

        private void start_camera_Click(object sender, EventArgs e)
        {
            string url = "";



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
