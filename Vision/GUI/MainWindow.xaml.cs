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
        public MainWindow()
        {
            InitializeComponent();

            var sensor = new Sensor();
            sensor.DepthImageUpdated += ImageUpdatedEventHandler;
        }

        private void ImageUpdatedEventHandler(object sender, Image image)
        {
            StreamingImage.Source = BitmapSource.Create(image.Width, image.Height, image.DpiX, image.DpiY, PixelFormats.Bgr32, null, image.Pixels, image.Stride);
        }
    }
}
