using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vision.Kinect;

namespace Vision.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly Sensor _sensor;

        private readonly Static2DMap _map;

        private readonly Stopwatch _frameWatch;

        private long _lastSecond;

        private int _framesShowed;

        public MainWindow()
        {
            _sensor = new Sensor();
            _map = new Static2DMap(_sensor);
            _frameWatch = Stopwatch.StartNew();

            InitializeComponent();

            ColorMenuItem.IsChecked = true;
            _sensor.ColorImageUpdated += ImageUpdatedEventHandler;

            ColorMenuItem.Checked += ViewMenuItemCheckedEventHandler;
            DepthMenuItem.Checked += ViewMenuItemCheckedEventHandler;
            MapMenuItem.Checked += ViewMenuItemCheckedEventHandler;
        }

        private void ImageUpdatedEventHandler(object sender, Image image)
        {
            var format = PixelFormats.Bgr32;
            switch (image.BitsPerPixel)
            {
                case 1:
                    format = PixelFormats.BlackWhite;
                    break;

                case 24:
                    format = PixelFormats.Bgr24;
                    break;

                case 32:
                    format = PixelFormats.Bgr32;
                    break;
            }

            StreamingImage.Source = BitmapSource.Create(image.Width, image.Height, image.DpiX, image.DpiY, format, null, image.Pixels, image.Stride);

            ++_framesShowed;

            var currentSecond = _frameWatch.ElapsedMilliseconds / 1000;
            if (currentSecond == _lastSecond)
                return;

            Title = string.Format("Vision: {0}fps", _framesShowed);

            _lastSecond = currentSecond;
            _framesShowed = 0;
        }

        private void ViewMenuItemCheckedEventHandler(object sender, RoutedEventArgs e)
        {
            _sensor.ColorImageUpdated -= ImageUpdatedEventHandler;
            _sensor.DepthImageUpdated -= ImageUpdatedEventHandler;
            _map.MapImageUpdated -= ImageUpdatedEventHandler;

            ColorMenuItem.IsChecked = ReferenceEquals(sender, ColorMenuItem);
            DepthMenuItem.IsChecked = ReferenceEquals(sender, DepthMenuItem);
            MapMenuItem.IsChecked = ReferenceEquals(sender, MapMenuItem);

            if (ColorMenuItem.IsChecked)
                _sensor.ColorImageUpdated += ImageUpdatedEventHandler;

            if (DepthMenuItem.IsChecked)
                _sensor.DepthImageUpdated += ImageUpdatedEventHandler;

            if (MapMenuItem.IsChecked)
                _map.MapImageUpdated += ImageUpdatedEventHandler;
        }
    }
}
