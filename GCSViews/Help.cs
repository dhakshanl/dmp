using MissionPlanner.Controls;
using MissionPlanner.Properties;
using MissionPlanner.Utilities;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using WebCamService;
using DirectShowLib;

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
           

            if (Program.WindowsStoreApp)
            {
                stop_camera.Visible = false;
                start_camera.Visible = false;
            }
        }

        void cam_camimage(Image camimage)
        {
            VideoBox.Image = camimage;
        }
        public static WebCamService.Capture cam { get; set; }
        public static bool MONO = false;
        public void start_camera_Click(object sender, EventArgs e)
        {
            try
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
        }

       

        private void Help_Load(object sender, EventArgs e)
        {
            //richTextBox1.Rtf = Resources.help_text;
            //ThemeManager.ApplyThemeTo(richTextBox1);
        }
       
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://firmware.ardupilot.org/Tools/MissionPlanner/upgrade/ChangeLog.txt");
        }

        private void stop_camera_Click(object sender, EventArgs e)
        {
            try
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
        }

        private void VideoBox_Click(object sender, EventArgs e)
        {

        }
    }
}