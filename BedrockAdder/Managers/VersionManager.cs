using System;
using System.IO;
using System.Windows.Controls;

namespace BedrockAdder.Managers
{
    internal class VersionManager
    {
        internal static void GetMinecraftVersions(ComboBox versionSelector)
        {
            versionSelector.Items.Clear();
            versionSelector.Items.Add("None");

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string versionsPath = Path.Combine(appDataPath, ".minecraft", "versions");

            if (Directory.Exists(versionsPath))
            {
                string[] versionDirs = Directory.GetDirectories(versionsPath);
                foreach (string dir in versionDirs)
                {
                    string versionName = Path.GetFileName(dir);
                    string jarPath = Path.Combine(dir, $"{versionName}.jar");
                    if (File.Exists(jarPath))
                    {
                        versionSelector.Items.Add(versionName);
                    }
                }
            }
            if (versionSelector.Items.Count > 1)
            {
                WindowManager.Main.ConvertButton.IsEnabled = true;
            }
            versionSelector.SelectedIndex = 0;
        }
    }
}
