using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RandomImageScreensaverWPF
{
    class SettingsManager
    {
        private const string RegistryKeyName = "RandomImageScreensaver";
        private const string PathValueName = "ImageDirectoryPath";
        private const string IntervalValueName = "ChangeIntervalSeconds";

        public static string ImageDirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        public static int ChangeIntervalSeconds { get; set; } = 10;

        static SettingsManager() => LoadSettings();

        public static void LoadSettings()
        {
            try
            {
                using (var key = GetRegistryEntry())
                {
                    ImageDirectoryPath = (string)key.GetValue(PathValueName, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
                    ChangeIntervalSeconds = (int)key.GetValue(IntervalValueName, 10);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        public static void SaveSettings()
        {
            try
            {
                using (var key = GetRegistryEntry())
                {
                    key.SetValue(PathValueName, ImageDirectoryPath);
                    key.SetValue(IntervalValueName, ChangeIntervalSeconds, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private static RegistryKey GetRegistryEntry() => Registry.CurrentUser.CreateSubKey($"Software\\{RegistryKeyName}");
    }
}
