using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using jp.nyatla.nyartoolkit.cs.core;
using jp.nyatla.nyartoolkit.cs.markersystem;
using Vision.Kinect;
using Vision.Processing;
using Image = Vision.Processing.Image;

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

        private SerialPort _servo;

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
            var map = new Static2DMap(Sensor.DepthFrameWidth, Sensor.DepthFrameHeight, Sensor.DepthFrameHorizontalAngle, Sensor.MaxDepth);
            map.MapImageUpdated += ImageUpdatedEventHandler;

            _markerSystem = new NyARMarkerSystem(new NyARMarkerSystemConfig(Sensor.MergedColorFrameWidth, Sensor.ColorFrameHeight));
            _markers.Add(_markerSystem.addARMarker(Path.GetFullPath("Data/patt.hiro"), 16, 25, 80));

            try
            {
                _sensor = new Sensor();
                _sensor.DepthDataReceived += (sender, data) => map.Update(data);

                UpdateImageVisibility();
            }
            catch (Exception exception)
            {
                var formatter = new BinaryFormatter();

                using (var stream = new FileStream("Data/colorImage.dat", FileMode.Open))
                {
                    var image = (Image)formatter.Deserialize(stream);
                    image.ImageType = ImageType.Color;
                    ImageUpdatedEventHandler(null, image);
                    var sensor = new NyARSensor(new NyARIntSize(image.Width, image.Height));
                    sensor.update(image);
                    RecognizeMarkers(sensor, image.Width, image.Height);
                }

                using (var stream = new FileStream("Data/depthImage.dat", FileMode.Open))
                {
                    var image = (Image)formatter.Deserialize(stream);
                    image.ImageType = ImageType.Depth;
                    ImageUpdatedEventHandler(null, image);
                }

                using (var stream = new FileStream("Data/depthData.dat", FileMode.Open))
                    map.Update((ushort[])formatter.Deserialize(stream));

                UpdateImageVisibility();

                MessageBox.Show(this, $"Using stored images.\r\n{exception.Message}", exception.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void ServoMenuItemLoadedEventHandler(object sender, RoutedEventArgs e)
        {
            var servoMenuItem = (MenuItem)sender;

            servoMenuItem.Items.Clear();

            var ports = SerialPort.GetPortNames();
            if (ports.Length == 0)
            {
                servoMenuItem.Items.Add(new MenuItem
                {
                    Header = "Empty",
                    IsEnabled = false
                });
            }
            else
            {
                foreach (var port in ports)
                {
                    var menuItem = new MenuItem { Header = port };
                    menuItem.Click += PortMenuClickEventHandler;

                    servoMenuItem.Items.Add(menuItem);
                }
            }
        }

        private void PortMenuClickEventHandler(object sender, RoutedEventArgs routedEventArgs)
        {
            var menuItem = (MenuItem)sender;
            ConnectToServo(menuItem.Header.ToString());
        }

        private void ConnectToServo(string port)
        {
            _servo = new SerialPort(port, 9600);
            _servo.Open();

            _servo.Write(new[] { (byte)90 }, 0, 1);

            Task.Run(() =>
            {
                var angle = 90;

                var isUp = true;
                while (true)
                {
                    var shift = 5;

                    if (isUp)
                    {
                        if (angle < 180)
                        {
                            angle += shift;
                        }
                        else
                        {
                            isUp = false;
                            continue;
                        }
                    }
                    else
                    {
                        if (angle > 0)
                        {
                            angle -= shift;
                        }
                        else
                        {
                            isUp = true;
                            continue;
                        }
                    }

                    _servo.Write(new[] { (byte)angle }, 0, 1);
                    Thread.Sleep(100);
                }
            });
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

        private async void RecognizeMarkers(NyARSensor sensor, int width, int height)
        {
            try
            {
                await Task.Run(async () =>
                {
                    _markerSystem.update(sensor);
                    if (_markerSystem.isExistMarker(_markers[0]))
                    {
                        var points = _markerSystem.getMarkerVertex2D(_markers[0]);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var visual = new DrawingVisual();
                            var context = visual.RenderOpen();
                            for (var i = 0; i < points.Length; i++)
                            {
                                var a = points[i];
                                var b = i == points.Length - 1 ? points[0] : points[i + 1];

                                context.DrawLine(new Pen(Brushes.DarkRed, 3), new Point(a.x, a.y), new Point(b.x, b.y));
                            }

                            context.Close();

                            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                            bitmap.Render(visual);

                            MarkerImage.Source = bitmap;
                            MarkerImage.Visibility = Visibility.Visible;
                        }, DispatcherPriority.Send);
                    }
                    else
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            MarkerImage.Visibility = Visibility.Hidden;
                        }, DispatcherPriority.Send);
                    }
                });
            }
            catch
            {
                // ignored
            }
        }

        private void ImageUpdatedEventHandler(object sender, Image image)
        {
            var format = PixelFormats.Gray8;
            var imageControl = ColorImage;

            switch (image.ImageType)
            {
                case ImageType.Map:
                    imageControl = MapImage;
                    break;

                case ImageType.Depth:
                    imageControl = DepthImage;
                    break;

                case ImageType.Color:
                    format = PixelFormats.Bgr32;
                    imageControl = ColorImage;
                    break;
            }

            switch (image.BitsPerPixel)
            {
                case 24:
                    format = PixelFormats.Bgr24;
                    break;

                case 32:
                    format = PixelFormats.Bgr32;
                    break;
            }

            if (image.ImageType == ImageType.Color)
                RecognizeMarkers(_sensor, image.Width, image.Height);

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
