using MissionPlanner.Controls;
using MissionPlanner.Properties;
using MissionPlanner.Utilities;
using System;
using System.Diagnostics;
using System.Windows.Forms;

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

        public void BUT_updatecheck_Click(object sender, EventArgs e)
        {
            try
            {
                if (Program.WindowsStoreApp)
                {
                    return;
                }
                Utilities.Update.CheckForUpdate(true);
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

        

        private void guna2CircleProgressBar1_ValueChanged(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabel1_Click(object sender, EventArgs e)
        {

        }

        private void guna2HtmlLabel2_Click(object sender, EventArgs e)
        {

        }
    }
}