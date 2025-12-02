using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RandomImageScreensaverWPF
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            PathTextBox?.Text = SettingsManager.ImageDirectoryPath;
            if (IntervalSlider != null)
            {
                IntervalSlider.Value = SettingsManager.ChangeIntervalSeconds;
                IntervalSlider.ValueChanged += IntervalSlider_ValueChanged;
            }
            IntervalLabel?.Content = $"Change Interval: {SettingsManager.ChangeIntervalSeconds} seconds";
        }

        private void IntervalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            IntervalLabel?.Content = $"Change Interval: {(int)IntervalSlider.Value} seconds";
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select any file in the desired image directory",
            };

            if (dialog.ShowDialog() == true)
            {
                PathTextBox?.Text = dialog.FolderName;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (PathTextBox != null && Directory.Exists(PathTextBox.Text))
            {
                SettingsManager.ImageDirectoryPath = PathTextBox.Text;
            }

            if (IntervalSlider != null)
            {
                SettingsManager.ChangeIntervalSeconds = (int)IntervalSlider.Value;
            }

            SettingsManager.SaveSettings();
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

    }
}
