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
            Image bgimage = camimage;
        }
        public void start_camera_Click(object sender, EventArgs e)
        {
            try
            {
                if (MainV2.MONO)
                    return;
                if (MainV2.cam == null)
                {
                    try
                    {
                        MainV2.cam = new WebCamService.Capture(Settings.Instance.GetInt32("video_device"), new AMMediaType());

                        MainV2.cam.Start();

                        MainV2.cam.camimage += new CamImage(cam_camimage);
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
            if (MainV2.cam != null)
            {
                MainV2.cam.Dispose();
                MainV2.cam = null;
            }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.ToString(), Strings.ERROR);
            }
        }
    }
}