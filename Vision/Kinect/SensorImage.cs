using System;
using System.Threading;
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

        public Image? DepthImage { get; private set; }

        public Image? ColorImage { get; private set; }

        internal void LoadDepthFrame(DepthFrame frame, ushort[] depthData, bool generateDepthImage, byte[] depthImageBytes, int depthImageHeight)
        {
            if (frame == null)
                return;

            DepthData = depthData;
            frame.CopyFrameDataToArray(DepthData);

            if (!generateDepthImage)
                return;
            
            var skipCount = (Sensor.DepthFrameHeight - depthImageHeight) / 2;
            var index = 0;

            for (var i = skipCount + 1; i < Sensor.DepthFrameHeight - skipCount; ++i)
            {
                for (var j = 0; j < Sensor.DepthFrameWidth; ++j)
                {
                    var offset = ((i * Sensor.DepthFrameWidth) + (Sensor.DepthFrameWidth - j));
                    var depth = Math.Min(DepthData[offset], Sensor.MaxDepth);
                    var percent = (double)depth / Sensor.MaxDepth;

                    var blueIntensity = percent > 0.5 ? (percent - 0.5) * 2 : 0;
                    var greenIntensity = percent > 0.5 ? 1 - percent : percent * 2;
                    var redIntensity = Math.Max(0, 1 - percent * 2);

                    depthImageBytes[index++] = (byte)(depth == 0 ? 0 : 255 * blueIntensity); // Blue
                    depthImageBytes[index++] = (byte)(depth == 0 ? 0 : 255 * greenIntensity); // Green
                    depthImageBytes[index++] = (byte)(depth == 0 ? 0 : 255 * redIntensity); // Red
                }
            }

            DepthImage = new Image
                {
                    Width = Sensor.DepthFrameWidth,
                    Height = depthImageHeight,
                    DpiX = 96.0,
                    DpiY = 96.0,
                    Pixels = depthImageBytes,
                    Stride = Sensor.DepthFrameWidth * 3,
                    BitsPerPixel = 24
                };
        }

        internal void LoadColorFrame(ColorFrame frame, byte[] pixels, byte[] newPixels, int currentWidth, bool mergeColorAndDepth)
        {
            if (frame == null)
                return;

            var width = Sensor.ColorFrameWidth;

            if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
                frame.CopyRawFrameDataToArray(pixels);
            else
                frame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Bgra);

            if (mergeColorAndDepth)
            {
                // TODO: Allow to configure merge parameters
                // Merge Point 
                // Margin: 98,98,98,98. Depth: 513, 424.828125, Color: 707, 397.6875
                width = currentWidth;

                var index = 0;
                var skipCount = (Sensor.ColorFrameWidth - width) / 2;

                for (var i = 0; i < Sensor.ColorFrameHeight; ++i)
                {
                    for (var j = 0; j < width; ++j)
                    {
                        var offset = ((i * Sensor.ColorFrameWidth) + (Sensor.ColorFrameWidth - skipCount - j)) * 4;
                        newPixels[index++] = pixels[offset];
                        newPixels[index++] = pixels[offset + 1];
                        newPixels[index++] = pixels[offset + 2];
                        newPixels[index++] = pixels[offset + 3];
                    }
                }

                pixels = newPixels;
            }

            ColorImage = new Image
                {
                    Width = width,
                    Height = Sensor.ColorFrameHeight,
                    DpiX = 96.0,
                    DpiY = 96.0,
                    Pixels = pixels,
                    Stride = width * 4,
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