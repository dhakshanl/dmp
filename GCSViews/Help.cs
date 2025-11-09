using DirectShowLib;
using MissionPlanner.Controls;
using MissionPlanner.Properties;
using MissionPlanner.Utilities;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using WebCamService;


namespace MissionPlanner.GCSViews
{
    public partial class Help : MyUserControl, IActivate
    {
        public Help()
        {
            InitializeComponent();
        }

        public void Activate()
        {
           

            /*if (Program.WindowsStoreApp)
            {
                stop_camera.Visible = false;
                start_camera.Visible = false;
            }*/
        }
        /*
        void cam_camimage(Image camimage)
        {
            VideoBox.Image = camimage;
        }
        public static WebCamService.Capture cam { get; set; }
        public static bool MONO = false;

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
        public void start_camera_Click(object sender, EventArgs e)
        {
            /*try
            {
                if (Help.MONO)
                    return;
                if (Help.cam == null)
                {
                    try
                    {
                        Help.cam = new WebCamService.Capture(Settings.Instance.GetInt32("video_device"), new AMMediaType());

                        Help.cam.Start();

                        Help.cam.camimage += new CamImage(cam_camimage);
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show("Camera Fail: " + ex.ToString(), Strings.ERROR);
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.ToString(), Strings.ERROR);
            }
            string url="";



            InputBox.Show("rtsp url", "Enter the url to the rtsp source url",ref url);

            if (!string.IsNullOrWhiteSpace(url))
                StartRTSP(url);
        }

       
        */
        private void Help_Load(object sender, EventArgs e)
        {
            //richTextBox1.Rtf = Resources.help_text;
            //ThemeManager.ApplyThemeTo(richTextBox1);
        }
       /*
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://firmware.ardupilot.org/Tools/MissionPlanner/upgrade/ChangeLog.txt");
        }

        private void stop_camera_Click(object sender, EventArgs e)
        {
            /* try
             {
                 if (Help.cam != null)
                 {
                     Help.cam.Dispose();
                     Help.cam = null;
                 }
             }
             catch (Exception ex)
             {
                 CustomMessageBox.Show(ex.ToString(), Strings.ERROR);
             }
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
        */
        private void VideoBox_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            // 1. Create a new instance of your UserControl.
            //    This NEW instance has all its own controls and its OWN click events ready to go.
            UcVideoStream newTabContent = new UcVideoStream();
            ThemeManager.ApplyThemeTo(newTabContent);
            // 2. Create a new TabPage
            TabPage newTabPage = new TabPage();
            newTabPage.Text = "Tab " + (tabControl1.TabCount + 1); // Example: "Tab 2"

            // 3. Add the UserControl to the new TabPage
            newTabPage.Controls.Add(newTabContent);
            newTabContent.Dock = DockStyle.Fill; // Make it fill the tab

            // 4. Add the new TabPage to your TabControl
            tabControl1.TabPages.Add(newTabPage);

            // 5. (Optional) Select the new tab
            tabControl1.SelectedTab = newTabPage;
        }

      
    }
}