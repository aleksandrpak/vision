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

        private readonly Dictionary<int, MarkerData> _markers;

        private WriteableBitmap _markersImage;

        private readonly Controller _servo;

        private readonly ManualResetEventSlim _mapWait;

        private int _shift;

        private bool _isRotating;
        private Static2DMap _map;

        public MainWindow()
        {
            DataContext = this;

            _markers = new Dictionary<int, MarkerData>();

            _servo = new Controller();
            _mapWait = new ManualResetEventSlim(true);
            _shift = 5;

            InitializeComponent();

            IsDrawingColorStream.IsChecked = true;
            IsDrawingDepthStream.IsChecked = true;

            IsDrawingColorStream.Checked += VisibilityCheckBoxesClicked;
            IsDrawingColorStream.Unchecked += VisibilityCheckBoxesClicked;
            IsDrawingDepthStream.Checked += VisibilityCheckBoxesClicked;
            IsDrawingDepthStream.Unchecked += VisibilityCheckBoxesClicked;
            IsDrawingCoordinates.Checked += VisibilityCheckBoxesClicked;
            IsDrawingCoordinates.Unchecked += VisibilityCheckBoxesClicked;
            IsDrawingObstacles.Checked += VisibilityCheckBoxesClicked;
            IsDrawingObstacles.Unchecked += VisibilityCheckBoxesClicked;
            IsDrawingOther.Checked += VisibilityCheckBoxesClicked;
            IsDrawingOther.Unchecked += VisibilityCheckBoxesClicked;

            InitializeSensor();

            DrawCoordinates();
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

        #region Map

        private void RangeBase_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            if (_map == null)
                return;

            var maxDepth = (ushort)args.NewValue;
            var shift = maxDepth / 5;
            _map.MaxDepth = maxDepth;

            Coordinate1.Text = Coordinate9.Text = $"{maxDepth / 10} см";
            Coordinate2.Text = Coordinate8.Text = $"{(maxDepth - shift) / 10} см";
            Coordinate3.Text = Coordinate7.Text = $"{(maxDepth - shift * 2) / 10} см";
            Coordinate4.Text = Coordinate6.Text = $"{(maxDepth - shift * 3) / 10} см";

            ObstacleMapImage.Source = _map.ObstaclesImage;
            OtherMapImage.Source = _map.OtherImage;
        }

        private void DrawCoordinates()
        {
            if (ObstacleMapImage.Source == null)
            {
                IsDrawingCoordinates.IsChecked = false;
                return;
            }

            IsDrawingCoordinates.IsChecked = true;

            var bitmap = BitmapFactory.New((int)ObstacleMapImage.Source.Width, (int)ObstacleMapImage.Source.Height);

            var width = bitmap.PixelWidth;
            var height = bitmap.PixelHeight;
            var center = width / 2;
            var shift = height / 5;

            for (var i = 0; i < 5; ++i)
            {
                var size = height + shift - shift * i;
                bitmap.DrawEllipseCentered(center, height, size, size, Colors.Black);
            }

            bitmap.DrawRectangle(0, 0, width, height, Colors.Black);

            for (var i = 0; i <= 8; ++i)
                bitmap.DrawLine(width * i / 8, 0, center, height, Colors.Black);

            for (var i = 0; i <= 6; ++i)
                bitmap.DrawLine(0, height * i / 6, center, height, Colors.Black);

            for (var i = 0; i <= 6; ++i)
                bitmap.DrawLine(width, height * i / 6, center, height, Colors.Black);

            CoordinateImage.Source = bitmap;
        }

        private void ButtonBase_OnClick(object sender, EventArgs args)
        {
            ushort height;
            if (!ushort.TryParse(HostHeightTextBox.Text, out height))
            {
                MessageBox.Show("Неверная высота тележки.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _map.HostHeight = height;
        }

        #endregion

        #region Kinect

        private async void InitializeSensor()
        {
            _markerSystem = new NyARMarkerSystem(new NyARMarkerSystemConfig(Sensor.ColorFrameWidth, Sensor.ColorFrameHeight));
            //_markers.Add(_markerSystem.addARMarker(Path.GetFullPath("Data/patt.hiro"), 16, 25, 80), new MarkerData { Filename = "patt.hiro", MarkerSize = 8, Width = 48, Depth = 40 });

            try
            {
                _sensor = new Sensor();
                DepthImage.Source = _sensor.DepthImage;
                ColorImage.Source = _sensor.ColorImage;

                _map = new Static2DMap(Sensor.DepthFrameWidth, Sensor.DepthFrameHeight, Sensor.DepthFrameHorizontalAngle, Sensor.DepthFrameVerticalAngle, Sensor.MaxDepth / 2, 20);
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
                    _map = new Static2DMap(Sensor.DepthFrameWidth, Sensor.DepthFrameHeight, Sensor.DepthFrameHorizontalAngle, Sensor.DepthFrameVerticalAngle, Sensor.MaxDepth / 2, 20);
                    var data = (ushort[])formatter.Deserialize(stream);
                    var dataFlipped = new ushort[data.Length];
                    data.FlipImageHorizontally(dataFlipped, Sensor.DepthFrameWidth);
                    await _map.Update(dataFlipped);
                }

                UpdateImageVisibility();

                MessageBox.Show(this, $"Используются сохраненные изображения.\r\n{exception.Message}", exception.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }

            ObstacleMapImage.Source = _map.ObstaclesImage;
            OtherMapImage.Source = _map.OtherImage;
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
            KinectStatusMenuItem.Header = isOpen ? "Подключен" : "Подключиться";
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

        private void VisibilityCheckBoxesClicked(object sender, RoutedEventArgs e)
        {
            UpdateImageVisibility();
        }

        private void UpdateImageVisibility()
        {
            if (_sensor != null)
            {
                _sensor.ColorImageUpdated -= ColorImageUpdatedEventHandler;

                if (IsDrawingColorStream.IsChecked.Value)
                    _sensor.ColorImageUpdated += ColorImageUpdatedEventHandler;

                _sensor.GenerateDepthImage = IsDrawingDepthStream.IsChecked.Value;
            }

            ColorImage.Visibility = IsDrawingColorStream.IsChecked.Value ? Visibility.Visible : Visibility.Hidden;
            DepthImage.Visibility = IsDrawingDepthStream.IsChecked.Value ? Visibility.Visible : Visibility.Hidden;

            if (IsDrawingColorStream.IsChecked.Value && IsDrawingDepthStream.IsChecked.Value)
                DepthImage.Opacity = 0.5;
            else
                DepthImage.Opacity = 1;

            var coordinatesVisibility = IsDrawingCoordinates.IsChecked.Value ? Visibility.Visible : Visibility.Hidden;
            CoordinateImage.Visibility = coordinatesVisibility;
            Coordinate1.Visibility = coordinatesVisibility;
            Coordinate2.Visibility = coordinatesVisibility;
            Coordinate3.Visibility = coordinatesVisibility;
            Coordinate4.Visibility = coordinatesVisibility;
            Coordinate5.Visibility = coordinatesVisibility;
            Coordinate6.Visibility = coordinatesVisibility;
            Coordinate7.Visibility = coordinatesVisibility;
            Coordinate8.Visibility = coordinatesVisibility;
            Coordinate9.Visibility = coordinatesVisibility;

            if (IsDrawingCoordinates.IsChecked.Value && CoordinateImage.Source == null)
                DrawCoordinates();

            ObstacleMapImage.Visibility = IsDrawingObstacles.IsChecked.Value ? Visibility.Visible : Visibility.Hidden;
            OtherMapImage.Visibility = IsDrawingOther.IsChecked.Value ? Visibility.Visible : Visibility.Hidden;
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
            public int Depth { get; set; }
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

                    context.Clear();

                    foreach (var marker in _markers)
                    {
                        if (!_markerSystem.isExistMarker(marker.Key) || !IsRecognizingMarkers.IsChecked.Value)
                        {
                            _map.RemoveMarker(marker.Key);
                            continue;
                        }

                        var points = _markerSystem.getMarkerVertex2D(marker.Key);
                        var matrix = _markerSystem.getMarkerMatrix(marker.Key);
                        var anglePoints = new NyARDoublePoint3d();
                        matrix.getZXYAngle(anglePoints);

                        var kinect = sensor as Sensor;
                        if (kinect != null)
                        {
                            var topLeft = points.OrderBy(p => p.x).ThenBy(p => p.y).First();
                            var topRight = points.OrderByDescending(p => p.x).ThenBy(p => p.y).First();
                            var bottomLeft = points.OrderBy(p => p.x).ThenByDescending(p => p.y).First();
                            var bottomRight = points.OrderByDescending(p => p.x).ThenByDescending(p => p.y).First();

                            var topLeftX = Sensor.ColorFrameWidth - topLeft.x;
                            var topLeftY = topLeft.y;
                            var topRightX = Sensor.ColorFrameWidth - topRight.x;
                            var topRightY = topRight.y;
                            var bottomLeftX = Sensor.ColorFrameWidth - bottomLeft.x;
                            var bottomLeftY = bottomLeft.y;
                            var bottomRightX = Sensor.ColorFrameWidth - bottomRight.x;
                            var bottomRightY = bottomRight.y;

                            kinect.GetDepthSpacePoint(ref topLeftX, ref topLeftY);
                            kinect.GetDepthSpacePoint(ref topRightX, ref topRightY);
                            kinect.GetDepthSpacePoint(ref bottomLeftX, ref bottomLeftY);
                            kinect.GetDepthSpacePoint(ref bottomRightX, ref bottomRightY);

                            var markerData = marker.Value;

                            var topLeftPoint = new Point(Sensor.DepthFrameWidth - topLeftX, topLeftY);
                            var topRightPoint = new Point(Sensor.DepthFrameWidth - topRightX, topRightY);
                            var bottomLeftPoint = new Point(Sensor.DepthFrameWidth - bottomLeftX, bottomLeftY);
                            var bottomRightPoint = new Point(Sensor.DepthFrameWidth - bottomRightX, bottomRightY);
                            _map.AddMarker(marker.Key, topLeftPoint, topRightPoint, bottomLeftPoint, bottomRightPoint, markerData.Width, markerData.Depth, anglePoints.y);
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
            RecognizeMarkers(_sensor, Sensor.ColorFrameWidth, Sensor.ColorFrameHeight);
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
                            Height = markerProperties.MarkerHeight,
                            Depth = markerProperties.MarkerDepth
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
                if (isUp ? angle < 120 : angle > 60)
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
                MessageBox.Show(this, $"Неудалось подключиться к сервоприводу.\r\n{exception.Message}", exception.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
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
                    Header = "Нет портов",
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
