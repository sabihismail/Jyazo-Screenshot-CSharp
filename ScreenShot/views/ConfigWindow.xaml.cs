using System.Windows;
using System.Windows.Controls.Primitives;
using ScreenShot.src;

namespace ScreenShot.views
{
    public partial class ConfigWindow
    {
        private readonly Config config;

        public ConfigWindow(Config config)
        {
            this.config = config;

            InitializeComponent();

            TxtServerEndpoint.Text = config.Server;
            TxtServerPassword.Text = config.ServerPassword;
            ChkGfycatUpload.IsChecked = config.EnableGfycatUpload;
            TxtGfycatClientID.Text = config.GfycatClientID;
            TxtGfycatClientSecret.Text = config.GfycatClientSecret;
            ChkOAuth2App.IsChecked = config.EnableOAuth2;

            if (IsChecked(ChkOAuth2App))
            {
                InputPasswordInfo.Visibility = Visibility.Collapsed;
            }
        }

        private void ChkGfycatUpload_OnClick(object sender, RoutedEventArgs e)
        {
            if (IsChecked(ChkGfycatUpload))
            {
                TxtGfycatClientID.IsEnabled = true;
                TxtGfycatClientSecret.IsEnabled = true;
            }
            else
            {
                TxtGfycatClientID.IsEnabled = false;
                TxtGfycatClientSecret.IsEnabled = false;

                TxtGfycatClientID.Text = "";
                TxtGfycatClientSecret.Text = "";
            }
        }

        private void BtnSave_OnClick(object sender, RoutedEventArgs e)
        {
            config.SaveConfig(TxtServerEndpoint.Text, 
                TxtServerPassword.Text, 
                IsChecked(ChkGfycatUpload),
                TxtGfycatClientID.Text, 
                TxtGfycatClientSecret.Text,
                IsChecked(ChkOAuth2App));

            Close();
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (IsChecked(ChkOAuth2App))
            {
                InputPasswordInfo.Visibility = Visibility.Collapsed;
            }
            else
            {
                InputPasswordInfo.Visibility = Visibility.Visible;
            }
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static bool IsChecked(ToggleButton chk)
        {
            return chk.IsChecked.HasValue && chk.IsChecked.Value;
        }
    }
}
