using DirectShowLib;
using MissionPlanner.Controls;
using MissionPlanner.Properties;
using MissionPlanner.Utilities;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using WebCamService;


namespace MissionPlanner.GCSViews

{
    public class CameraInfo
    {
        public int CameraId { get; set; }
        public string StreamUrl { get; set; }
        public TabPage Tab { get; set; }
        public UcVideoStream StreamControl { get; set; }
    }
    public partial class Help : MyUserControl, IActivate
    {

        public Help()
        {
            InitializeComponent();
        }

        public void Activate()
        {

        }
        private List<CameraInfo> cameras = new List<CameraInfo>();

        private void button1_Click(object sender, EventArgs e)
        {
            // Prompt for RTSP URL
            //string url = "rtsp://192.168.144.25:8554/main.264";
            //if (InputBox.Show("RTSP URL", "Enter camera URL", ref url) != DialogResult.OK || string.IsNullOrWhiteSpace(url))
                //return;

            UcVideoStream newTabContent = new UcVideoStream();
            ThemeManager.ApplyThemeTo(newTabContent);

            TabPage newTabPage = new TabPage();
            newTabPage.Text = $"Cam {cameras.Count + 1}";
            newTabPage.Controls.Add(newTabContent);
            newTabContent.Dock = DockStyle.Fill;

            tabControl1.TabPages.Add(newTabPage);
            tabControl1.SelectedTab = newTabPage;

            // Add to camera list.
            var camInfo = new CameraInfo()
            {
                CameraId = cameras.Count + 1,
                Tab = newTabPage,
                StreamControl = newTabContent
            };
            cameras.Add(camInfo);

            // Wire stream buttons if exposed
            if (newTabContent.StartButton != null)
                newTabContent.StartButton.Click  += new System.EventHandler(newTabContent.stop_camera_Click);
            if (newTabContent.StopButton != null)
                newTabContent.StopButton.Click += (s, ev) => newTabContent.StopStream();

            RenumberCameraTabs();
        }



        private void RenumberCameraTabs()
        {
            for (int i = 0; i < cameras.Count; i++)
            {
                cameras[i].CameraId = i + 1;
                cameras[i].Tab.Text = $"Cam {i + 1}";
            }
        }

        private void red_close_button_Click(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab == null) return;

            TabPage currentTab = tabControl1.SelectedTab;

            // Find the camera matching this tab
            int camIndex = cameras.FindIndex(c => c.Tab == currentTab);
            if (camIndex >= 0)
            {
                cameras[camIndex].StreamControl.StopStream(); // Stop video, clean up
                cameras.RemoveAt(camIndex);
            }

            tabControl1.TabPages.Remove(currentTab);
            currentTab.Dispose();

            RenumberCameraTabs();
        }



    }
}