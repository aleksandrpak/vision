using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using jp.nyatla.nyartoolkit.cs.core;
using jp.nyatla.nyartoolkit.cs.markersystem;
using Servo;
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

        private Static2DMap _map;

        private WriteableBitmap _markersImage;

        private readonly Controller _servo;

        private readonly ManualResetEventSlim _mapWait;

        private int _shift;

        private bool _isRotating;

        public MainWindow()
        {
            _markers = new List<int>();

            _servo = new Controller();
            _mapWait = new ManualResetEventSlim(true);
            _shift = 5;

            InitializeComponent();

            ColorMenuItem.IsChecked = true;
            DepthMenuItem.IsChecked = true;

            ColorMenuItem.Checked += ViewMenuItemCheckedEventHandler;
            ColorMenuItem.Unchecked += ViewMenuItemCheckedEventHandler;
            DepthMenuItem.Checked += ViewMenuItemCheckedEventHandler;
            DepthMenuItem.Unchecked += ViewMenuItemCheckedEventHandler;

            InitializeSensor();
        }

        #region Kinect

        private void InitializeSensor()
        {
            _markerSystem = new NyARMarkerSystem(new NyARMarkerSystemConfig(Sensor.MergedColorFrameWidth, Sensor.ColorFrameHeight));
            _markers.Add(_markerSystem.addARMarker(Path.GetFullPath("Data/patt.hiro"), 16, 25, 80));

            try
            {
                _sensor = new Sensor();
                _sensor.DepthDataReceived += (sender, data) => _map.Update(data);
                DepthImage.Source = _sensor.DepthImage;
                ColorImage.Source = _sensor.ColorImage;

                _map = new Static2DMap(Sensor.DepthFrameWidth, _sensor.CurrentDepthHeight, Sensor.DepthFrameHorizontalAngle, Sensor.DepthFrameVerticalAngle * ((double)_sensor.CurrentDepthHeight / Sensor.DepthFrameHeight), Sensor.MaxDepth);

                UpdateImageVisibility();
            }
            catch (Exception exception)
            {
                var formatter = new BinaryFormatter();

                using (var stream = new FileStream("Data/colorImage.dat", FileMode.Open))
                {
                    var image = (Image)formatter.Deserialize(stream);
                    image.ImageType = ImageType.Color;
                    LoadImage(image);
                    var sensor = new NyARSensor(new NyARIntSize(image.Width, image.Height));
                    sensor.update(image);
                    RecognizeMarkers(sensor, image.Width, image.Height);
                }

                using (var stream = new FileStream("Data/depthImage.dat", FileMode.Open))
                {
                    var image = (Image)formatter.Deserialize(stream);
                    image.ImageType = ImageType.Depth;
                    LoadImage(image);
                }

                using (var stream = new FileStream("Data/depthData.dat", FileMode.Open))
                {
                    _map = new Static2DMap(Sensor.DepthFrameWidth, Sensor.DepthFrameHeight, Sensor.DepthFrameHorizontalAngle, Sensor.DepthFrameVerticalAngle, Sensor.MaxDepth);
                    _map.Update((ushort[])formatter.Deserialize(stream));
                }

                UpdateImageVisibility();

                MessageBox.Show(this, $"Using stored images.\r\n{exception.Message}", exception.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }

            MapImage.Source = _map.Image;
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

        private void LoadImage(Image image)
        {
            var format = PixelFormats.Bgr32;
            var imageControl = ColorImage;

            switch (image.ImageType)
            {
                case ImageType.Depth:
                    imageControl = DepthImage;
                    break;

                case ImageType.Color:
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
        }

        private void ViewMenuItemCheckedEventHandler(object sender, RoutedEventArgs e)
        {
            UpdateImageVisibility();
        }

        private void UpdateImageVisibility()
        {
            if (_sensor != null)
            {
                _sensor.ColorImageUpdated -= ColorImageUpdatedEventHandler;

                if (ColorMenuItem.IsChecked)
                    _sensor.ColorImageUpdated += ColorImageUpdatedEventHandler;

                _sensor.GenerateDepthImage = DepthMenuItem.IsChecked;
            }

            ColorImage.Visibility = ColorMenuItem.IsChecked ? Visibility.Visible : Visibility.Hidden;
            DepthImage.Visibility = DepthMenuItem.IsChecked ? Visibility.Visible : Visibility.Hidden;

            if (ColorMenuItem.IsChecked && DepthMenuItem.IsChecked)
                DepthImage.Opacity = 0.5;
            else
                DepthImage.Opacity = 1;
        }

        #endregion

        #region Markers

        private async void RecognizeMarkers(NyARSensor sensor, int width, int height)
        {
            if (sensor == null)
                return;

            if (_markersImage == null || Math.Abs(_markersImage.Width - width) > double.Epsilon || Math.Abs(_markersImage.Height - height) > double.Epsilon)
            {
                _markersImage = BitmapFactory.New(width, height);
                MarkerImage.Source = _markersImage;
            }

            if (!Monitor.TryEnter(_markersImage))
                return;

            using (var context = _markersImage.GetBitmapContext(ReadWriteMode.ReadWrite))
            {
                try
                {
                    context.Clear();

                    var pixelWidth = context.Width;
                    var pixelHeight = context.Height;
                    var color = WriteableBitmapExtensions.ConvertColor(Colors.DarkRed);

                    await Task.Run(() => _markerSystem.update(sensor));

                    foreach (var marker in _markers)
                    {
                        if (!_markerSystem.isExistMarker(marker))
                            continue;

                        var points = _markerSystem.getMarkerVertex2D(marker);
                        _markersImage.Clear();

                        for (var j = 0; j < points.Length; ++j)
                        {
                            var a = points[j];
                            var b = j == points.Length - 1 ? points[0] : points[j + 1];

                            WriteableBitmapExtensions.DrawLineAa(context, pixelWidth, pixelHeight, a.x, a.y, b.x, b.y, color, 2);
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }

            Monitor.Exit(_markersImage);
        }

        private void ColorImageUpdatedEventHandler(object sender, EventArgs args)
        {
            RecognizeMarkers(_sensor, _sensor.CurrentColorWidth, Sensor.ColorFrameHeight);
        }

        #endregion

        #region Servo

        private void RotateServo()
        {
            int angle = _servo.Angle;
            _map.SetAngle(angle);

            var isUp = true;
            while (_isRotating && _servo.IsConnected)
            {
                if (isUp ? angle < 180 : angle > 0)
                {
                    angle += (isUp ? _shift : -_shift);
                }
                else
                {
                    isUp = !isUp;
                    continue;
                }

                try
                {
                    _servo.Rotate((byte)angle);
                }
                catch
                {
                    _map.DisconnectServo();
                    _isRotating = false;
                    return;
                }

                _map.SetAngle(angle);

                _mapWait.Reset();
                _mapWait.Wait();
            }
        }

        private void ConnectToServo(string port)
        {
            try
            {
                _servo.Connect(port);

                _mapWait.Reset();
                _map.ConnectServo(_mapWait);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, $"Failed to connect to servo.\r\n{exception.Message}", exception.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShiftDegreeMenuItemClickEventHandler(object sender, RoutedEventArgs e)
        {
            var menuItem = (MenuItem)sender;
            _shift = Convert.ToByte(menuItem.Header.ToString().TrimEnd('°'));
        }

        private void ServoAutoStartMenuIteckClickEventHandler(object sender, RoutedEventArgs e)
        {
            _isRotating = true;
            Task.Run(() => RotateServo());
        }

        private void ServoAutoStopMenuIteckClickEventHandler(object sender, RoutedEventArgs e)
        {
            _isRotating = false;
        }

        private void ServoManualClickEventHandler(object sender, RoutedEventArgs e)
        {
            if (!_servo.IsConnected)
                return;

            var isLeft = ReferenceEquals(sender, ServoManualLeft);
            var angle = _servo.Angle + (isLeft ? -_shift : _shift);

            if (angle < 0 || angle > 180)
                return;

            _servo.Rotate((byte)angle);
        }

        private void ServoMenuItemLoadedEventHandler(object sender, RoutedEventArgs e)
        {
            var servoMenuItem = (MenuItem)sender;

            servoMenuItem.Items.Clear();

            var ports = SerialPort.GetPortNames();
            Array.Sort(ports);

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

        #endregion
    }
}
