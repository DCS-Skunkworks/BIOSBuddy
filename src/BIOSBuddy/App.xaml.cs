using System;
using System.Windows;
using BIOSBuddy.Properties;

namespace BIOSBuddy
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App 
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                /*
                 * Load previous application/user settings.
                 */
                if (Settings.Default.UpgradeRequired)
                {
                    Settings.Default.Upgrade();
                    Settings.Default.UpgradeRequired = false;
                    Settings.Default.Save();
                }

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error starting BIOSBuddy." + Environment.NewLine + ex.Message + Environment.NewLine + ex.StackTrace);
                Current.Shutdown(0);
                Environment.Exit(0);
            }
        }
    }


}
