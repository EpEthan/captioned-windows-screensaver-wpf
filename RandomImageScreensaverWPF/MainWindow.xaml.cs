using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RandomImageScreensaverWPF
{
    public partial class MainWindow : Window
    {
        private static readonly string[] _imageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif"];
        private const double IMG_START_SCALE = 1.05;
        private const double IMG_BASE_SCALE = 1.0;
        private const double MINIMUM_ANIMATION_DURATION = 2;
        private const double POLL_IMAGES_INTERVAL_MS = 100;


        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private readonly List<string> _imageFiles = new List<string>();
        private int _currentImageIndex = -1;
        private readonly Random _random = new Random();
        private volatile int _runningJobCount = 0;

        // P/Invoke for embedding into preview window (Required for /p)
        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int GWL_STYLE = -16;
        private const int WS_CHILD = 0x40000000;
        private const int SWP_NOZORDER = 0x0004;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;


        public MainWindow(IntPtr? previewHandle = null)
        {
            InitializeComponent();

            HandleScreenSaverViewMode(previewHandle);

            LoadImages();

            // Setup the timer for image transitions
            _timer.Interval = TimeSpan.FromMilliseconds(POLL_IMAGES_INTERVAL_MS);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void HandleScreenSaverViewMode(IntPtr? previewHandle = null)
        {
            this.Background = Brushes.Black;

            if (previewHandle.HasValue && previewHandle.Value != IntPtr.Zero)
            {
                // --- Screen Saver Preview Mode (/p) ---
                IntPtr windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                SetParent(windowHandle, previewHandle.Value);
                SetWindowLong(windowHandle, GWL_STYLE, GetWindowLong(windowHandle, GWL_STYLE) | WS_CHILD);
                SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOZORDER | SWP_NOMOVE | SWP_NOSIZE);

                this.AllowsTransparency = false;
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Normal;
                this.ShowInTaskbar = false;
            }
            else
            {
                // --- Screen Saver Full-Screen Mode (/s) ---
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
                this.ShowInTaskbar = false;
                this.Cursor = System.Windows.Input.Cursors.None;
                this.Loaded += MainWindow_Loaded;
                this.Closing += MainWindow_Closing;
                this.KeyDown += (s, e) => System.Windows.Application.Current.Shutdown();
                this.MouseDown += (s, e) => System.Windows.Application.Current.Shutdown();
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PhotoDisplay.Height = double.NaN;
            PhotoDisplay.Width = double.NaN;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _timer.Stop();
        }

        private void LoadImages()
        {
            _imageFiles.Clear();
            if (!Directory.Exists(SettingsManager.ImageDirectoryPath))
            {
                MessageBox.Show($"Image directory not found: {SettingsManager.ImageDirectoryPath}\n\nFalling back to My Pictures.", "Screen Saver Error", MessageBoxButton.OK, MessageBoxImage.Error);
                SettingsManager.ImageDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            }

            Task.Run(() => LoadImagesBackground(SettingsManager.ImageDirectoryPath));
        }

        private void LoadImagesBackground(string root)
        {
            _runningJobCount++;

            try
            {
                var images = Directory.EnumerateFiles(root, searchPattern: "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => _imageExtensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()));
                _imageFiles.AddRange(images);

                foreach (string directory in Directory.EnumerateDirectories(root))
                {
                    Task.Run(() => { LoadImagesBackground(directory); });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while loading images: {ex.Message}", "Screen Saver Error", MessageBoxButton.OK, MessageBoxImage.Error);
            } finally { 
                _runningJobCount--; 
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_imageFiles.Count == 0)
            {
                if (_runningJobCount == 0 && System.Windows.Application.Current != null)
                {
                    MessageBox.Show($"No images found in: {SettingsManager.ImageDirectoryPath}", "Screen Saver Error", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }

                return;
            }

            if (_timer.Interval.TotalSeconds != SettingsManager.ChangeIntervalSeconds)
            {
                _timer.Interval = TimeSpan.FromSeconds(SettingsManager.ChangeIntervalSeconds);
            }

            DisplayNextImage();
        }

        private void DisplayNextImage()
        {
            if (_imageFiles.Count == 0) return;

            _currentImageIndex = GetNewRandomImageIndex();
            string imagePath = _imageFiles[_currentImageIndex];

            try
            {
                // Load the image
                var bitmap = LoadImage(imagePath);

                PhotoDisplay.Source = bitmap;  // TODO

                // Start the calm zoom-in animation
                AnimateZoomIn();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load image: {imagePath}. Error: {ex.Message}");
                // Remove the failed image path and try the next one
                _imageFiles.RemoveAt(_currentImageIndex);
                _currentImageIndex = -1;
                DisplayNextImage();
            }
        }

        private int GetNewRandomImageIndex()
        {
            int newIndex;
            do
            {
                newIndex = _random.Next(_imageFiles.Count);
            } while (newIndex == _currentImageIndex && _imageFiles.Count > 1);

            return newIndex;
        }

        private static BitmapImage LoadImage(string imagePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            return bitmap;
        }

        private void AnimateZoomIn()
        {
            // Reset scale transformation (ensures we start from a known state)
            if (PhotoDisplay.RenderTransform is not ScaleTransform)
            {
                PhotoDisplay.RenderTransform = new ScaleTransform(IMG_BASE_SCALE, IMG_BASE_SCALE);
            }

            var scaleXAnimation = CreateScaleAnimation();
            var scaleYAnimation = CreateScaleAnimation();

            // Create a Storyboard to run the animations concurrently
            var storyboard = new Storyboard();
            storyboard.Children.Add(scaleXAnimation);
            storyboard.Children.Add(scaleYAnimation);

            Storyboard.SetTarget(scaleXAnimation, PhotoDisplay);
            Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.ScaleX"));

            Storyboard.SetTarget(scaleYAnimation, PhotoDisplay);
            Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.ScaleY"));

            storyboard.Begin();
        }

        private Timeline CreateScaleAnimation()
        {
            double duration = Math.Max(MINIMUM_ANIMATION_DURATION, SettingsManager.ChangeIntervalSeconds);

            return new DoubleAnimation(IMG_START_SCALE, IMG_BASE_SCALE, TimeSpan.FromSeconds(duration))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
        }
    }
}