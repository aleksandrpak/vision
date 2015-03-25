using System;
using System.Linq;
using Microsoft.Kinect;

namespace Vision.Kinect
{
    public sealed class Sensor
    {
        #region Fields

        private readonly KinectSensor _sensor;

        private MultiSourceFrameReader _reader;

        private EventHandler<Image> _colorImageUpdated;

        private EventHandler<Image> _depthImageUpdated;

        private int _colorImageSubscribers;

        private int _depthImageSubscribers;

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

            if (_depthImageSubscribers > 0)
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

        #endregion

        #region Events

        private void AddEvent(ref EventHandler<Image> eventHandler, EventHandler<Image> value, ref int subscribers)
        {
            lock (_sensor) // We don't need high performance in this operation
            {
                eventHandler += value;
                ++subscribers;

                InitializeReader();
            }
        }

        private void RemoveEvent(ref EventHandler<Image> eventHandler, EventHandler<Image> value, ref int subscribers)
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
                AddEvent(ref _depthImageUpdated, value, ref _depthImageSubscribers);
            }
            remove
            {
                RemoveEvent(ref _depthImageUpdated, value, ref _depthImageSubscribers);
            }
        }

        #endregion
    }
}
