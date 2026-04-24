using System;
using System.Windows;
using AI_AOI.Config;

namespace AI_AOI.Views
{
    /// <summary>
    /// Interaction logic for SoftwareSettings.xaml
    /// </summary>
    public partial class SoftwareSettings : Window
    {
        public SoftwareSettings()
        {
            InitializeComponent();
            SoftwareSettingsManager.EnsureLoaded();
            DataContext = SoftwareSettingsManager.Current;
            Closed += SoftwareSettings_Closed;
        }

        private void SoftwareSettings_Closed(object sender, EventArgs e)
        {
            SoftwareSettingsManager.Save();
        }
    }
}
