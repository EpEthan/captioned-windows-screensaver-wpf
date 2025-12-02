using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace RandomImageScreensaverWPF
{
    class Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            SettingsManager.LoadSettings();
            string command = args.Length > 0 ? args[0].ToLowerInvariant().TrimStart('/') : string.Empty;

            switch (command)
            {
                case "c": // Configuration
                    if (args.Length > 1) // Optional second argument: parent HWND for non-modal config
                    {
                        // For screensaver, /c is typically run modally without a parent window
                    }
                    RunApplication(new SettingsWindow { Topmost = true });
                    break;

                case "p": // Preview (requires HWND)
                    if (args.Length > 1 && int.TryParse(args[1], out int handleValue))
                    {
                        IntPtr previewHandle = new(handleValue);
                        RunApplication(new MainWindow(previewHandle));
                    }
                    else
                    {
                        // Invalid argument for preview mode, show settings or full screen by default
                        RunApplication(new SettingsWindow { Topmost = true });
                    }
                    break;

                case "s": // Full-Screen (Default mode)
                default:
                    if (command != "s" && !string.IsNullOrEmpty(command))
                    {
                        // If an unknown command is passed, treat as 's' unless it was just a mouse move/keyboard event
                        // Standard screensaver behavior is to just run in full screen if argument is unknown/missing
                    }
                    RunApplication(new MainWindow());
                    break;
            }
        }

        // Helper to run the application using the WPF Application class
        private static void RunApplication(Window startWindow)
        {
            var app = new System.Windows.Application
            {
                ShutdownMode = ShutdownMode.OnMainWindowClose
            };
            app.Run(startWindow);
        }
    }
}
