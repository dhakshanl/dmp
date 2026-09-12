using System.Drawing;
using System.Windows.Forms;

namespace MissionPlanner.Controls.Dashboard
{
    /// <summary>
    /// Flat dark action button for the mission dashboard.
    /// Exists as a subclass rather than a configured System.Windows.Forms.Button because
    /// Utilities\ThemeManager.ApplyTheme matches on `ctl.GetType() == typeof(Button)` and
    /// would otherwise repaint every button ForeColor=Black/BackColor=ButBG on screen
    /// switch — which would strip the Abort/E-Stop button of its red, the one visual cue
    /// that distinguishes an emergency control from an ordinary one.
    /// </summary>
    public class DashboardButton : Button
    {
        public DashboardButton()
        {
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            BackColor = Color.FromArgb(40, 40, 60);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            FlatAppearance.BorderColor = Color.FromArgb(70, 70, 100);
        }
    }
}
