using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using jp.nyatla.nyartoolkit.cs.core;
using jp.nyatla.nyartoolkit.cs.markersystem;
using Microsoft.Win32;
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

        private Dictionary<int, MarkerData> _markers;

        private Static2DMap _map;

        private WriteableBitmap _markersImage;

        private readonly Controller _servo;

        private readonly ManualResetEventSlim _mapWait;

        private int _shift;

        private bool _isRotating;

        public MainWindow()
        {
            _markers = new Dictionary<int, MarkerData>();

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

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            base.OnPreviewKeyUp(e);

            switch (e.Key)
            {
                case Key.C:
                    _map.Clear();
                    break;

                case Key.Left:
                    ManualRotateServo(true);
                    break;

                case Key.Right:
                    ManualRotateServo(false);
                    break;
            }
        }

        #region Kinect

        private async void InitializeSensor()
        {
            _markerSystem = new NyARMarkerSystem(new NyARMarkerSystemConfig(Sensor.MergedColorFrameWidth, Sensor.ColorFrameHeight));
            //_markers.Add(_markerSystem.addARMarker(Path.GetFullPath("Data/patt.hiro"), 16, 25, 80), new MarkerData { Filename = "patt.hiro", MarkerSize = 8, Width = 48, Height = 40 });

            try
            {
                _sensor = new Sensor();
                DepthImage.Source = _sensor.DepthImage;
                ColorImage.Source = _sensor.ColorImage;

                _map = new Static2DMap(Sensor.DepthFrameWidth, _sensor.CurrentDepthHeight, Sensor.DepthFrameHorizontalAngle, Sensor.DepthFrameVerticalAngle * ((double)_sensor.CurrentDepthHeight / Sensor.DepthFrameHeight), Sensor.MaxDepth, 0);
                _sensor.DepthDataReceiver = _map.Update;

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
                    _map = new Static2DMap(Sensor.DepthFrameWidth, Sensor.DepthFrameHeight, Sensor.DepthFrameHorizontalAngle, Sensor.DepthFrameVerticalAngle, Sensor.MaxDepth, 0);
                    var data = (ushort[])formatter.Deserialize(stream);
                    var dataFlipped = new ushort[data.Length];
                    data.FlipImageHorizontally(dataFlipped, Sensor.DepthFrameWidth);
                    await _map.Update(dataFlipped);
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

        private void ClearMapClickEventHandler(object sender, RoutedEventArgs e)
        {
            _map.Clear();
        }

        #endregion

        #region Markers

        private struct MarkerData
        {
            public string Filename { get; set; }
            public int MarkerSize { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

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
                    var pixelWidth = context.Width;
                    var pixelHeight = context.Height;
                    var color = WriteableBitmapExtensions.ConvertColor(Colors.DarkRed);

                    await Task.Run(() => _markerSystem.update(sensor));

                    foreach (var marker in _markers)
                    {
                        if (!_markerSystem.isExistMarker(marker.Key))
                        {
                            _map.RemoveMarker(marker.Key);
                            continue;
                        }

                        var points = _markerSystem.getMarkerVertex2D(marker.Key);
                        _markersImage.Clear();

                        var kinect = sensor as Sensor;
                        if (kinect != null)
                        {
                            var shift = (Sensor.ColorFrameWidth - width) / 2;
                            var topLeft = points.OrderBy(p => p.x).ThenBy(p => p.y).First();
                            var topRight = points.OrderByDescending(p => p.x).ThenBy(p => p.y).First();

                            var topLeftX = Sensor.ColorFrameWidth - (topLeft.x + shift);
                            var topLeftY = topLeft.y;
                            var topRightX = Sensor.ColorFrameWidth - (topRight.x + shift);
                            var topRightY = topRight.y;

                            kinect.GetDepthSpacePoint(ref topLeftX, ref topLeftY);
                            kinect.GetDepthSpacePoint(ref topRightX, ref topRightY);

                            var multiplier = (marker.Value.Width / marker.Value.MarkerSize);
                            topRightX = (topLeftX + (topRightX - topLeftX) * multiplier);

                            _map.AddMarker(marker.Key, Sensor.DepthFrameWidth - topLeftX, topLeftY, Sensor.DepthFrameWidth - topRightX, topRightY, marker.Value.Height);
                        }

                        for (var j = 0; j < points.Length; ++j)
                        {
                            var a = points[j];
                            var b = j == points.Length - 1 ? points[0] : points[j + 1];

                            WriteableBitmapExtensions.DrawLineAa(context, pixelWidth, pixelHeight, a.x, a.y, b.x, b.y, color, 3);
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

        private void AddMarkerClickEventHandler(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = false,
                CheckFileExists = true,
                InitialDirectory = Path.GetFullPath("Data")
            };

            var result = openFileDialog.ShowDialog(this);
            if (result.Value)
            {
                var markerProperties = new MarkerProperties();
                var propertieResult = markerProperties.ShowDialog();
                if (propertieResult != null && propertieResult.Value)
                {
                    _markers.Add(
                        _markerSystem.addARMarker(openFileDialog.FileName, 16, 25, markerProperties.MarkerSize),
                        new MarkerData
                        {
                            Filename = Path.GetFileName(openFileDialog.FileName),
                            MarkerSize = markerProperties.MarkerSize,
                            Width = markerProperties.MarkerWidth,
                            Height = markerProperties.MarkerHeight
                        });
                }
            }
        }

        #endregion

        #region Servo

        private void AutoRotateServo()
        {
            if (_isRotating)
                return;

            int angle = _servo.Angle;
            _map.SetAngle(angle);

            var isUp = true;
            _isRotating = true;

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

                if (!RotateServo((byte)angle))
                    return;
            }
        }

        private bool RotateServo(byte angle)
        {
            _mapWait.Set();

            try
            {
                _servo.Rotate(angle);
            }
            catch
            {
                _map.DisconnectServo();
                _isRotating = false;
                return false;
            }

            _map.SetAngle(angle);

            _mapWait.Reset();
            _mapWait.Wait();
            return true;
        }

        private async void ConnectToServo(string port)
        {
            try
            {
                await Task.Run(() =>
                {
                    _servo.Connect(port);
                    _map.ConnectServo(_mapWait);
                });
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
            Task.Run(() => AutoRotateServo());
        }

        private void ServoAutoStopMenuIteckClickEventHandler(object sender, RoutedEventArgs e)
        {
            _isRotating = false;
        }

        private void ServoManualClickEventHandler(object sender, RoutedEventArgs e)
        {
            ManualRotateServo(ReferenceEquals(sender, ServoManualLeft));
        }

        private async void ManualRotateServo(bool isLeft)
        {
            if (!_servo.IsConnected)
                return;

            var angle = _servo.Angle + (isLeft ? _shift : -_shift);

            if (angle < 0 || angle > 180)
                return;

            await Task.Run(() => RotateServo((byte)angle));
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
