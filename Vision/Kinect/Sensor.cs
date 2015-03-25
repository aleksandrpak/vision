using System;
using Microsoft.Kinect;

namespace Vision.Kinect
{
    public sealed class Sensor
    {
        public Sensor()
        {
            var sensor = KinectSensor.GetDefault();

            if (sensor == null)
                throw new InvalidOperationException("Failed to initialize Kinect sensor.");

            sensor.Open();
            var reader = sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color);

            reader.MultiSourceFrameArrived += MultiSourceFrameArrivedEventHandler;
        }

        private void MultiSourceFrameArrivedEventHandler(object sender, MultiSourceFrameArrivedEventArgs args)
        {
            var reference = args.FrameReference.AcquireFrame();

            using (var frame = reference.ColorFrameReference.AcquireFrame())
                HandleColorFrame(frame);

            using (var frame = reference.DepthFrameReference.AcquireFrame())
                HandleDepthFrame(frame);
        }

        private void HandleDepthFrame(DepthFrame frame)
        {
            if (frame == null)
                return;

            var width = frame.FrameDescription.Width;
            var height = frame.FrameDescription.Height;

            var minDepth = frame.DepthMinReliableDistance;
            var maxDepth = frame.DepthMaxReliableDistance;

            var depthData = new ushort[width * height];
            var pixelData = new byte[width * height * (32 + 7) / 8];

            frame.CopyFrameDataToArray(depthData);

            var colorIndex = 0;
            for (var depthIndex = 0; depthIndex < depthData.Length; ++depthIndex)
            {
                var depth = depthData[depthIndex];
                var intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);

                pixelData[colorIndex++] = intensity; // Blue
                pixelData[colorIndex++] = intensity; // Green
                pixelData[colorIndex++] = intensity; // Red

                ++colorIndex;
            }

            var stride = width * 32 / 8;

            RaiseDepthImageUpdated(new Image
                {
                    Width = width,
                    Height = height,
                    DpiX = 96.0,
                    DpiY = 96.0,
                    Pixels = pixelData,
                    Stride = stride
                });
        }

        private void HandleColorFrame(ColorFrame frame)
        {
            if (frame == null)
                return;

            var width = frame.FrameDescription.Width;
            var height = frame.FrameDescription.Height;

            var pixels = new Byte[width * height * ((32 + 7) / 8)];

            if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
                frame.CopyRawFrameDataToArray(pixels);
            else
                frame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Bgra);

            var stride = width * 32 / 8;

            RaiseColorImageUpdated(new Image
                {
                    Width = width,
                    Height = height,
                    DpiX = 96.0,
                    DpiY = 96.0,
                    Pixels = pixels,
                    Stride = stride
                });
        }
        private void RaiseColorImageUpdated(Image image)
        {
            ColorImageUpdated?.Invoke(this, image);
        }

        private void RaiseDepthImageUpdated(Image image)
        {
            DepthImageUpdated?.Invoke(this, image);
        }

        public event EventHandler<Image> ColorImageUpdated;

        public event EventHandler<Image> DepthImageUpdated;
    }
}
