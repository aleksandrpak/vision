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

        public MainWindow()
        {
            _sensor = new Sensor();

            InitializeComponent();
            
            ColorMenuItem.IsChecked = true;
            _sensor.ColorImageUpdated += ImageUpdatedEventHandler;

            ColorMenuItem.Checked += ViewMenuItemCheckedEventHandler;
            DepthMenuItem.Checked += ViewMenuItemCheckedEventHandler;
            MapMenuItem.Checked += ViewMenuItemCheckedEventHandler;
        }

        private void ImageUpdatedEventHandler(object sender, Image image)
        {
            StreamingImage.Source = BitmapSource.Create(image.Width, image.Height, image.DpiX, image.DpiY, PixelFormats.Bgr32, null, image.Pixels, image.Stride);
        }

        private void ViewMenuItemCheckedEventHandler(object sender, RoutedEventArgs e)
        {
            _sensor.ColorImageUpdated -= ImageUpdatedEventHandler;
            _sensor.DepthImageUpdated -= ImageUpdatedEventHandler;

            ColorMenuItem.IsChecked = ReferenceEquals(sender, ColorMenuItem);
            DepthMenuItem.IsChecked = ReferenceEquals(sender, DepthMenuItem);
            MapMenuItem.IsChecked = ReferenceEquals(sender, MapMenuItem);

            if (ColorMenuItem.IsChecked)
                _sensor.ColorImageUpdated += ImageUpdatedEventHandler;

            if (DepthMenuItem.IsChecked)
                _sensor.DepthImageUpdated += ImageUpdatedEventHandler;
        }
    }
}
