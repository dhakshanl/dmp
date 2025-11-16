using BSE.Windows.Forms.Properties;
using System;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;

namespace MissionPlanner
{
    public partial class Splash : Form
    {
        public Splash()
        {
            InitializeComponent();

            string strVersion = typeof(Splash).GetType().Assembly.GetName().Version.ToString();

            TXT_version.Text = "Version: " + "0.1.0"; // +" Build " + strVersion;

            //Console.WriteLine(strVersion);

            if (Program.Logo != null)
            {
                //pictureBox1.BackgroundImage = MissionPlanner.Properties.Resources.bgdark;
                //pictureBox1.Image = Image.FromFile(@"C:\Users\lnara\Desktop\testmp\dmp\dmp_logo.png");
                //pictureBox1.Visible = true;
            }

            Console.WriteLine("Splash .ctor");
        }

        private void Splash_Load(object sender, EventArgs e)
        {

        }
    }
}