using System;
using System.Threading;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using Vision.Processing;

namespace Vision.Kinect
{
    public struct SensorImage : IDisposable
    {
        private readonly object _lockObject;

        public SensorImage(object lockObject)
            : this()
        {
            _lockObject = lockObject;
            IsLocked = Monitor.TryEnter(_lockObject);
        }

        public bool IsLocked { get; }

        public ushort[] DepthData { get; private set; }

        public Image? ColorImage { get; private set; }

        internal void LoadDepthFrame(DepthFrame frame, ushort[] depthData, ushort[] depthDataCropped, ushort[] depthDataFlipped, bool generateDepthImage, WriteableBitmap image, int depthImageHeight)
        {
            if (frame == null)
                return;

            DepthData = depthDataFlipped;
            frame.CopyFrameDataToArray(depthData);

            depthData.CropImage(depthDataCropped, Sensor.DepthFrameWidth, Sensor.DepthFrameHeight, Sensor.DepthFrameWidth, depthImageHeight);
            depthDataCropped.FlipImageHorizontally(depthDataFlipped, Sensor.DepthFrameWidth);

            if (!generateDepthImage)
                return;

            unsafe
            {
                using (var context = image.GetBitmapContext(ReadWriteMode.ReadWrite))
                {
                    var pixelWidth = context.Width;

                    for (var i = 0; i < depthImageHeight; ++i)
                    {
                        for (var j = 0; j < Sensor.DepthFrameWidth; ++j)
                        {
                            var depth = Math.Min(DepthData[i * Sensor.DepthFrameWidth + j], Sensor.MaxDepth);
                            var percent = (double)depth / Sensor.MaxDepth;

                            var blueIntensity = percent > 0.5 ? (percent - 0.5) * 2 : 0;
                            var greenIntensity = percent > 0.5 ? 1 - percent : percent * 2;
                            var redIntensity = Math.Max(0, 1 - percent * 2);

                            context.Pixels[i * pixelWidth + j] = -16777216 |
                                                               (int)(depth == 0 ? 0 : 255 * redIntensity) << 16 |
                                                               (int)(depth == 0 ? 0 : 255 * greenIntensity) << 8 |
                                                               (int)(depth == 0 ? 0 : 255 * blueIntensity);
                        }
                    }
                }
            }
        }

        internal void LoadColorFrame(ColorFrame frame, byte[] pixels, byte[] pixelsCropped, byte[] pixelsFlipped, WriteableBitmap image, int currentWidth)
        {
            if (frame == null)
                return;

            if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
                frame.CopyRawFrameDataToArray(pixels);
            else
                frame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Bgra);

            pixels.CropImage(pixelsCropped, Sensor.ColorFrameWidth, Sensor.ColorFrameHeight, currentWidth, Sensor.ColorFrameHeight, 4);
            pixelsCropped.FlipImageHorizontally(pixelsFlipped, currentWidth, 4);

            // TODO: Allow to configure merge parameters
            // Merge Point 
            // Margin: 98,98,98,98. Depth: 513, 424.828125, Color: 707, 397.6875

            unsafe
            {
                using (var context = image.GetBitmapContext(ReadWriteMode.ReadWrite))
                {
                    var pixelWidth = context.Width;

                    for (var i = 0; i < Sensor.ColorFrameHeight; ++i)
                    {
                        for (var j = 0; j < currentWidth; ++j)
                        {
                            var offset = i * currentWidth * 4 + j * 4;

                            context.Pixels[i * pixelWidth + j] = -16777216 |
                                                               pixelsFlipped[offset + 2] << 16 |
                                                               pixelsFlipped[offset + 1] << 8 |
                                                               pixelsFlipped[offset];
                        }
                    }
                }
            }

            ColorImage = new Image
            {
                ImageType = ImageType.Color,
                Width = currentWidth,
                Height = Sensor.ColorFrameHeight,
                DpiX = 96.0,
                DpiY = 96.0,
                Pixels = pixelsFlipped,
                Stride = currentWidth * 4,
                BitsPerPixel = 32
            };
        }

        void IDisposable.Dispose()
        {
            if (IsLocked)
                Monitor.Exit(_lockObject);
        }
    }
}