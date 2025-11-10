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
          
        }
     
        private void VideoBox_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
          
            UcVideoStream newTabContent = new UcVideoStream();
            ThemeManager.ApplyThemeTo(newTabContent);
           
            TabPage newTabPage = new TabPage();
            newTabPage.Text = "Tab " + (tabControl1.TabCount + 1);

            newTabPage.Controls.Add(newTabContent);
            newTabContent.Dock = DockStyle.Fill; 
           
            tabControl1.TabPages.Add(newTabPage);

            tabControl1.SelectedTab = newTabPage;
        }

        private void ucVideoStream1_Load(object sender, EventArgs e)
        {

        }

        private void transparentPanel1_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}