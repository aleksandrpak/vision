using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using jp.nyatla.nyartoolkit.cs.markersystem;
using Vision.Processing;
using Vision.Kinect;

namespace Vision.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private Sensor _sensor;

        private NyARMarkerSystem _markerSystem;

        private readonly List<int> _markers;

        private readonly Stopwatch _frameWatch;

        private long _lastSecond;

        private int _framesShowed;

        public MainWindow()
        {
            _markers = new List<int>();

            _frameWatch = Stopwatch.StartNew();

            InitializeComponent();

            ColorMenuItem.IsChecked = true;
            DepthMenuItem.IsChecked = true;

            ColorMenuItem.Checked += ViewMenuItemCheckedEventHandler;
            ColorMenuItem.Unchecked += ViewMenuItemCheckedEventHandler;
            DepthMenuItem.Checked += ViewMenuItemCheckedEventHandler;
            DepthMenuItem.Unchecked += ViewMenuItemCheckedEventHandler;

            InitializeSensor();
        }

        private void InitializeSensor()
        {
            try
            {
                _sensor = new Sensor();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, exception.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var map = new Static2DMap(Sensor.DepthFrameWidth, Sensor.DepthFrameHeight, Sensor.DepthFrameHorizontalAngle, Sensor.MaxDepth);
            _sensor.DepthDataReceived += (sender, data) => map.Update(data);

            map.MapImageUpdated += ImageUpdatedEventHandler;

            _markerSystem = new NyARMarkerSystem(new NyARMarkerSystemConfig(_sensor.CurrentColorWidth, Sensor.ColorFrameHeight));
            _markers.Add(_markerSystem.addARMarker(Path.GetFullPath("Data/patt.hiro"), 16, 25, 80));

            UpdateImageVisibility();
        }

        private void KinectStatusMenuItemClickEventHandler(object sender, RoutedEventArgs e)
        {
            InitializeSensor();
            UpdateKinectStatus();
        }

        private void KinectStatusMenuItemLoadedEventHandler(object sender, RoutedEventArgs e)
        {
            UpdateKinectStatus();
        }

        private void UpdateKinectStatus()
        {
            var isOpen = _sensor != null && _sensor.IsConnected;

            KinectStatusMenuItem.IsEnabled = !isOpen;
            KinectStatusMenuItem.Header = isOpen ? "Connected" : "Connect";
        }

        // TODO: Code for adjusting of merging
        //protected override void OnPreviewKeyDown(KeyEventArgs e)
        //{
        //    base.OnPreviewKeyDown(e);

        //    var m = DepthImage.Margin;

        //    switch (e.Key)
        //    {
        //        case Key.Right:
        //            DepthImage.Margin = new Thickness(m.Left + 1, m.Top, m.Right - 1, m.Bottom);
        //            break;
        //        case Key.Left:
        //            DepthImage.Margin = new Thickness(m.Left - 1, m.Top, m.Right + 1, m.Bottom);
        //            break;
        //        case Key.Up:
        //            DepthImage.Margin = new Thickness(m.Left, m.Top - 1, m.Right, m.Bottom + 1);
        //            break;
        //        case Key.Down:
        //            DepthImage.Margin = new Thickness(m.Left, m.Top + 1, m.Right, m.Bottom - 1);
        //            break;
        //        case Key.OemPlus:
        //            DepthImage.Margin = new Thickness(m.Left - 1, m.Top - 1, m.Right - 1, m.Bottom - 1);
        //            break;
        //        case Key.OemMinus:
        //            DepthImage.Margin = new Thickness(m.Left + 1, m.Top + 1, m.Right + 1, m.Bottom + 1);
        //            break;
        //    }

        //    Console.WriteLine("Margin: {0}. Depth: {1}, Color: {2}", DepthImage.Margin, new Size(DepthImage.ActualWidth, DepthImage.ActualHeight), new Size(ColorImage.ActualWidth, ColorImage.ActualHeight));
        //}

        private void ImageUpdatedEventHandler(object sender, Image image)
        {
            var format = PixelFormats.Bgr32;
            var imageControl = ColorImage;

            switch (image.BitsPerPixel)
            {
                case 8:
                    format = PixelFormats.Gray8;
                    imageControl = MapImage;
                    break;

                case 24:
                    format = PixelFormats.Bgr24;
                    imageControl = DepthImage;
                    break;

                case 32:
                    format = PixelFormats.Bgr32;
                    imageControl = ColorImage;
                    break;
            }

            if (format == PixelFormats.Bgr32)
            {
                try
                {
                    MarkerImage.Visibility = Visibility.Hidden;

                    _markerSystem.update(_sensor);
                    if (_markerSystem.isExistMarker(_markers[0]))
                    {
                        var points = _markerSystem.getMarkerVertex2D(_markers[0]);
                        var visual = new DrawingVisual();
                        var context = visual.RenderOpen();
                        for (var i = 0; i < points.Length; i++)
                        {
                            var a = points[i];
                            var b = i == points.Length - 1 ? points[0] : points[i + 1];

                            context.DrawLine(new Pen(Brushes.DarkRed, 3), new Point(a.x, a.y), new Point(b.x, b.y));
                        }

                        context.Close();

                        var bitmap = new RenderTargetBitmap(image.Width, image.Height, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(visual);
                        MarkerImage.Source = bitmap;
                        MarkerImage.Visibility = Visibility.Visible;
                    }
                }
                catch
                {
                    // ignored
                }
            }

            imageControl.Source = BitmapSource.Create(image.Width, image.Height, image.DpiX, image.DpiY, format, null, image.Pixels, image.Stride);

            ++_framesShowed;

            var currentSecond = _frameWatch.ElapsedMilliseconds / 1000;
            if (currentSecond == _lastSecond)
                return;

            Title = $"Vision: {_framesShowed} frames processed in total";

            _lastSecond = currentSecond;
            _framesShowed = 0;
        }

        private void ViewMenuItemCheckedEventHandler(object sender, RoutedEventArgs e)
        {
            UpdateImageVisibility();
        }

        private void UpdateImageVisibility()
        {
            if (_sensor != null)
            {
                _sensor.ColorImageUpdated -= ImageUpdatedEventHandler;
                _sensor.DepthImageUpdated -= ImageUpdatedEventHandler;

                if (ColorMenuItem.IsChecked)
                    _sensor.ColorImageUpdated += ImageUpdatedEventHandler;

                if (DepthMenuItem.IsChecked)
                    _sensor.DepthImageUpdated += ImageUpdatedEventHandler;
            }

            MarkerImage.Visibility = Visibility.Hidden;
            ColorImage.Visibility = ColorMenuItem.IsChecked ? Visibility.Visible : Visibility.Hidden;
            DepthImage.Visibility = DepthMenuItem.IsChecked ? Visibility.Visible : Visibility.Hidden;

            if (ColorMenuItem.IsChecked && DepthMenuItem.IsChecked)
                DepthImage.Opacity = 0.5;
            else
                DepthImage.Opacity = 1;
        }
    }
}
