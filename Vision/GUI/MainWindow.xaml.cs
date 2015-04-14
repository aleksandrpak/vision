using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vision.Kinect;
using Image = Vision.Kinect.Image;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Drawing.Point;

namespace Vision.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly Sensor _sensor;

        private readonly Stopwatch _frameWatch;

        private long _lastSecond;

        private int _framesShowed;

        private Bitmap _modelImage;

        public MainWindow()
        {
            _sensor = new Sensor();
            var map = new Static2DMap(_sensor);
            _frameWatch = Stopwatch.StartNew();

            _modelImage = new Bitmap(Path.GetFullPath("../../../checkboard.png"));

            InitializeComponent();

            ColorMenuItem.IsChecked = true;
            //DepthMenuItem.IsChecked = true;

            //map.MapImageUpdated += ImageUpdatedEventHandler;

            ColorMenuItem.Checked += ViewMenuItemCheckedEventHandler;
            ColorMenuItem.Unchecked += ViewMenuItemCheckedEventHandler;
            DepthMenuItem.Checked += ViewMenuItemCheckedEventHandler;
            DepthMenuItem.Unchecked += ViewMenuItemCheckedEventHandler;

            UpdateImageVisibility();
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
                    format = PixelFormats.Gray8;
                    imageControl = ColorImage;
                    break;
            }

            var source = BitmapSource.Create(image.Width, image.Height, image.DpiX, image.DpiY, format, null, image.Pixels, image.Stride);
            imageControl.Source = source;

            long matchTime = -1;

            if (ReferenceEquals(imageControl, ColorImage))
            {
                //Matcher.Foo(_modelImage, GetBitmap(source));
                var recognizedImage = Matcher.Draw(_modelImage, GetBitmap(source), out matchTime);
                RecongnizedImage.Source = Imaging.CreateBitmapSourceFromHBitmap(
                    recognizedImage.GetHbitmap(),
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }

            ++_framesShowed;

            var currentSecond = _frameWatch.ElapsedMilliseconds / 1000;
            if (currentSecond == _lastSecond)
                return;

            Title = string.Format("Vision: {0} frames processed in total. {1}",
                _framesShowed,
                matchTime >= 0 ? string.Format("Matched in {0}", matchTime) : string.Empty);

            _lastSecond = currentSecond;
            _framesShowed = 0;
        }

        private Bitmap GetBitmap(BitmapSource source)
        {
            Bitmap bmp = new Bitmap(
                source.PixelWidth,
                source.PixelHeight,
                PixelFormat.Format8bppIndexed);
            BitmapData data = bmp.LockBits(
              new Rectangle(Point.Empty, bmp.Size),
              ImageLockMode.WriteOnly,
              PixelFormat.Format8bppIndexed);
            source.CopyPixels(
              Int32Rect.Empty,
              data.Scan0,
              data.Height * data.Stride,
              data.Stride);
            bmp.UnlockBits(data);
            return bmp;
        }

        private void ViewMenuItemCheckedEventHandler(object sender, RoutedEventArgs e)
        {
            UpdateImageVisibility();
        }

        private void UpdateImageVisibility()
        {
            _sensor.ColorImageUpdated -= ImageUpdatedEventHandler;
            _sensor.DepthImageUpdated -= ImageUpdatedEventHandler;

            if (ColorMenuItem.IsChecked)
                _sensor.ColorImageUpdated += ImageUpdatedEventHandler;

            if (DepthMenuItem.IsChecked)
                _sensor.DepthImageUpdated += ImageUpdatedEventHandler;

            ColorImage.Visibility = ColorMenuItem.IsChecked ? Visibility.Visible : Visibility.Hidden;
            DepthImage.Visibility = DepthMenuItem.IsChecked ? Visibility.Visible : Visibility.Hidden;

            if (ColorMenuItem.IsChecked && DepthMenuItem.IsChecked)
                DepthImage.Opacity = 0.5;
            else
                DepthImage.Opacity = 1;
        }
    }
}
