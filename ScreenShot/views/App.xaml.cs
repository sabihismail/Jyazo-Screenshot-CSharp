using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using ScreenShot.src;
using ScreenShot.src.capture;
using ScreenShot.src.tools;

namespace ScreenShot.views
{
    public partial class App
    {
        private NotifyIcon taskbarIcon;

        private readonly Settings settings = new Settings();
        private readonly Config config = new Config();

        public App()
        {
            InitializeComponent();

            ConfigureTaskbar();
            WindowInformation.BeginObservingWindows();
        }

        private void ConfigureTaskbar()
        {
            var strip = new ContextMenuStrip();

            var menuCaptureImage = new ToolStripMenuItem("Capture Image", null, (sender, args) =>
            {
                var captureImage = new CaptureImage(settings, config);

                captureImage.Show();
            });

            var menuCaptureGIF = new ToolStripMenuItem("Capture GIF", null, (sender, args) =>
            {
                var captureGIF = new CaptureGIF(settings, config);

                captureGIF.Show();
            });

            var menuViewAllImages = new ToolStripMenuItem("View All Images", null, (sender, args) =>
            {
                if (!settings.saveAllImages) return;

                Process.Start("explorer.exe", settings.saveDirectory);
            });

            var menuSettings = new ToolStripMenuItem("Settings", null, (sender, args) =>
            {
                var settingsWindow = new SettingsWindow(settings, config);

                settingsWindow.Show();
            });

            var menuExit = new ToolStripMenuItem("Exit", null, (sender, args) =>
            {
                Shutdown();
            });

            strip.Items.Add(menuCaptureImage);
            strip.Items.Add(menuCaptureGIF);
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(menuViewAllImages);
            strip.Items.Add(menuSettings);
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(menuExit);

            strip.Items[0].Font = new Font(strip.Items[0].Font, strip.Items[0].Font.Style | System.Drawing.FontStyle.Bold);
            strip.Items[1].Font = new Font(strip.Items[1].Font, strip.Items[1].Font.Style | System.Drawing.FontStyle.Bold);

            taskbarIcon = new NotifyIcon
            {
                ContextMenuStrip = strip
            };

            taskbarIcon.MouseClick += (sender, args) =>
            {
                if (args.Button == MouseButtons.Right)
                {
                    menuCaptureImage.Text = $"Capture Image ({settings.captureImageShortcut.Replace(" ", " + ")})";
                    menuCaptureGIF.Text = $"Capture GIF ({settings.captureImageShortcut.Replace(" ", " + ")})";
                }
                else if (args.Button == MouseButtons.Left)
                {
                    var captureImage = new CaptureImage(settings, config);

                    captureImage.Show();
                }
            };

            using (var stream = GetResourceStream(new Uri("/resources/icon.ico", UriKind.Relative))?.Stream)
            {
                taskbarIcon.Icon = new Icon(stream ?? throw new InvalidOperationException("icon not found"));
            }

            taskbarIcon.Visible = true;
        }
    }
}
