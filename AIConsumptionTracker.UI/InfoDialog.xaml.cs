using System;
using System.Reflection;
using System.Windows;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;

namespace AIConsumptionTracker.UI
{
    public partial class InfoDialog : Window
    {
        public InfoDialog()
        {
            InitializeComponent();
            LoadInfo();
        }

        private void LoadInfo()
        {
            // Application version
            var appVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            if (appVersion != null)
            {
                InternalVersionText.Text = $"v{appVersion.Major}.{appVersion.Minor}.{appVersion.Build}";
            }

            // .NET Runtime version
            DotNetVersionText.Text = RuntimeInformation.FrameworkDescription;

            // Operating System
            OsVersionText.Text = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";

            // Architecture
            ArchitectureText.Text = RuntimeInformation.ProcessArchitecture.ToString();

            // Machine name
            MachineNameText.Text = Environment.MachineName;

            // Current user
            UserNameText.Text = Environment.UserName;

            // Configuration File Path
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ai-consumption-tracker", "auth.json");
            ConfigLinkText.Text = configPath;
            ConfigLinkText.ToolTip = configPath;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Header_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void ConfigPath_Click(object sender, RoutedEventArgs e)
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ai-consumption-tracker", "auth.json");
            if (File.Exists(configPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{configPath}\"",
                    UseShellExecute = true
                });
            }
            else
            {
                var directory = Path.GetDirectoryName(configPath);
                if (directory != null && Directory.Exists(directory))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{directory}\"",
                        UseShellExecute = true
                    });
                }
            }
        }
    }
}
