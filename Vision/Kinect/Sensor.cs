using System;
using System.Linq;
using Microsoft.Kinect;

namespace Vision.Kinect
{
    public sealed class Sensor
    {
        #region Fields

        public const ushort MaxDepth = 8000;

        public const int DepthFrameWidth = 512;

        public const int DepthFrameHeight = 424;

        private readonly KinectSensor _sensor;

        private MultiSourceFrameReader _reader;

        private EventHandler<Image> _colorImageUpdated;

        private EventHandler<Image> _depthImageUpdated;

        private EventHandler<byte[]> _depthDataReceived;

        private int _colorImageSubscribers;

        private int _depthSubscribers;

        #endregion

        public Sensor()
        {
            _sensor = KinectSensor.GetDefault();

            if (_sensor == null)
                throw new InvalidOperationException("Failed to initialize Kinect sensor.");

            _sensor.Open();
        }

        private void MultiSourceFrameArrivedEventHandler(object sender, MultiSourceFrameArrivedEventArgs args)
        {
            var reference = args.FrameReference.AcquireFrame();

            using (var frame = reference.ColorFrameReference.AcquireFrame())
                HandleColorFrame(frame);

            using (var frame = reference.DepthFrameReference.AcquireFrame())
                HandleDepthFrame(frame);
        }

        #region Frame Handling

        private void InitializeReader()
        {
            var types = FrameSourceTypes.None;

            if (_colorImageSubscribers > 0)
                types |= FrameSourceTypes.Color;

            if (_depthSubscribers > 0)
                types |= FrameSourceTypes.Depth;

            DestroyReader(types);
            CreateReader(types);
        }

        private void CreateReader(FrameSourceTypes types)
        {
            if (_reader == null ? types == FrameSourceTypes.None : _reader.FrameSourceTypes == types)
                return;

            _reader = _sensor.OpenMultiSourceFrameReader(types);
            _reader.MultiSourceFrameArrived += MultiSourceFrameArrivedEventHandler;
        }

        private void DestroyReader(FrameSourceTypes types)
        {
            if (_reader == null || _reader.FrameSourceTypes == types)
                return;

            _reader.MultiSourceFrameArrived -= MultiSourceFrameArrivedEventHandler;
            _reader.Dispose();
            _reader = null;
        }

        private void HandleDepthFrame(DepthFrame frame)
        {
            if (frame == null)
                return;

            var depthData = new ushort[DepthFrameWidth * DepthFrameHeight];
            var pixelData = new byte[DepthFrameWidth * DepthFrameHeight * 3];

            frame.CopyFrameDataToArray(depthData);

            var colorIndex = 0;
            for (var depthIndex = 0; depthIndex < depthData.Length; ++depthIndex)
            {
                var depth = Math.Min(depthData[depthIndex], MaxDepth);
                var percent = (double)depth / MaxDepth;

                var blueIntensity = percent > 0.5 ? (percent - 0.5) * 2 : 0;
                var greenIntensity = percent > 0.5 ? 1 - percent : percent * 2;
                var redIntensity = Math.Max(0, 1 - percent * 2);

                pixelData[colorIndex++] = (byte)(depth == 0 ? 0 : 255 * blueIntensity); // Blue
                pixelData[colorIndex++] = (byte)(depth == 0 ? 0 : 255 * greenIntensity); // Green
                pixelData[colorIndex++] = (byte)(depth == 0 ? 0 : 255 * redIntensity); // Red
            }
            
            RaiseDepthImageUpdated(new Image
                {
                    Width = DepthFrameWidth,
                    Height = DepthFrameHeight,
                    DpiX = 96.0,
                    DpiY = 96.0,
                    Pixels = pixelData,
                    Stride = DepthFrameWidth * 3,
                    BitsPerPixel = 24
                });
        }

        private void HandleColorFrame(ColorFrame frame)
        {
            if (frame == null)
                return;

            var width = frame.FrameDescription.Width;
            var height = frame.FrameDescription.Height;

            var pixels = new byte[width * height * 4];

            if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
                frame.CopyRawFrameDataToArray(pixels);
            else
                frame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Bgra);

            RaiseColorImageUpdated(new Image
                {
                    Width = width,
                    Height = height,
                    DpiX = 96.0,
                    DpiY = 96.0,
                    Pixels = pixels,
                    Stride = width * 4,
                    BitsPerPixel = 32
                });
        }

        #endregion

        #region Events

        private void AddEvent<T>(ref EventHandler<T> eventHandler, EventHandler<T> value, ref int subscribers)
        {
            lock (_sensor) // We don't need high performance in this operation
            {
                eventHandler += value;
                ++subscribers;

                InitializeReader();
            }
        }

        private void RemoveEvent<T>(ref EventHandler<T> eventHandler, EventHandler<T> value, ref int subscribers)
        {
            lock (_sensor) // We don't need high performance in this operation
            {
                if (eventHandler == null)
                    return;

                if (!eventHandler.GetInvocationList().Contains(value))
                    return;

                // ReSharper disable once DelegateSubtraction
                eventHandler -= value;
                --subscribers;

                InitializeReader();
            }
        }

        private void RaiseColorImageUpdated(Image image)
        {
            _colorImageUpdated?.Invoke(this, image);
        }

        private void RaiseDepthImageUpdated(Image image)
        {
            _depthImageUpdated?.Invoke(this, image);
        }

        private void RaiseDepthDataReceived(byte[] data)
        {
            _depthDataReceived?.Invoke(this, data);
        }

        public event EventHandler<Image> ColorImageUpdated
        {
            add
            {
                AddEvent(ref _colorImageUpdated, value, ref _colorImageSubscribers);
            }
            remove
            {
                RemoveEvent(ref _colorImageUpdated, value, ref _colorImageSubscribers);
            }
        }

        public event EventHandler<Image> DepthImageUpdated
        {
            add
            {
                AddEvent(ref _depthImageUpdated, value, ref _depthSubscribers);
            }
            remove
            {
                RemoveEvent(ref _depthImageUpdated, value, ref _depthSubscribers);
            }
        }

        public event EventHandler<byte[]> DepthDataReceived
        {
            add
            {
                AddEvent(ref _depthDataReceived, value, ref _depthSubscribers);
            }
            remove
            {
                RemoveEvent(ref _depthDataReceived, value, ref _depthSubscribers);
            }
        }

        #endregion
    }
}
