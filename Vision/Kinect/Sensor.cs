using System;
using System.Linq;
using System.Threading;
using jp.nyatla.nyartoolkit.cs.core;
using jp.nyatla.nyartoolkit.cs.markersystem;
using Microsoft.Kinect;
using Vision.Processing;

namespace Vision.Kinect
{
    public sealed class Sensor : NyARSensor
    {
        #region Fields

        public const ushort MaxDepth = 8000;

        public const double DepthFrameHorizontalAngle = 70;

        public const double DepthFrameVerticalAngle = 70;

        public const int DepthFrameWidth = 512;

        public const int DepthFrameHeight = 424;

        public const int ColorFrameWidth = 1920;

        public const int ColorFrameHeight = 1080;

        private readonly KinectSensor _sensor;

        private MultiSourceFrameReader _reader;

        private bool _mergeColorAndDepth;

        private readonly ushort[] _depthData;

        private byte[] _depthImageBytes;

        private readonly byte[] _colorImageData;

        private byte[] _colorImageBytes;

        private EventHandler<Image> _colorImageUpdated;

        private EventHandler<Image> _depthImageUpdated;

        private EventHandler<ushort[]> _depthDataReceived;

        private int _colorImageSubscribers;

        private int _depthImageSubscribers;

        private int _depthDataSubscribers;

        #endregion

        public Sensor()
            : base(new NyARIntSize((int)((ColorFrameWidth / 707.0) * 513.0), ColorFrameHeight))
        {
            _sensor = KinectSensor.GetDefault();

            MergeColorAndDepth = true;
            _depthData = new ushort[DepthFrameWidth * DepthFrameHeight];
            _depthImageBytes = new byte[DepthFrameWidth * CurrentDepthHeight * 3];

            _colorImageData = new byte[ColorFrameWidth * ColorFrameHeight * 4];
            _colorImageBytes = new byte[CurrentColorWidth * ColorFrameHeight * 4];

            if (_sensor == null)
                throw new InvalidOperationException("Failed to initialize Kinect sensor.");

            _sensor.Open();

            Thread.Sleep(1000); // Let it initialize

            if (!_sensor.IsOpen || !_sensor.IsAvailable)
                throw new InvalidOperationException("Failed to open Kinect sensor.");

        }

        public bool IsConnected => _sensor.IsOpen && _sensor.IsAvailable;

        public bool MergeColorAndDepth
        {
            get { return _mergeColorAndDepth; }
            set
            {
                if (_mergeColorAndDepth == value)
                    return;

                _mergeColorAndDepth = value;
                _depthImageBytes = new byte[DepthFrameWidth * CurrentDepthHeight * 3];
                _colorImageBytes = new byte[CurrentColorWidth * ColorFrameHeight * 4];
            }
        }

        public int CurrentColorWidth => MergeColorAndDepth ? (int)((ColorFrameWidth / 707.0) * 513.0) : ColorFrameWidth;

        // TODO: Allow to configure merge parameters
        // Merge Point 
        // Margin: 98,98,98,98. Depth: 513, 424.828125, Color: 707, 397.6875
        public int CurrentDepthHeight => MergeColorAndDepth ? 397 : DepthFrameHeight;

        private void MultiSourceFrameArrivedEventHandler(object sender, MultiSourceFrameArrivedEventArgs args)
        {
            using (var image = new SensorImage(this))
            {
                if (!image.IsLocked)
                    return;

                var reference = args.FrameReference.AcquireFrame();

                if (_colorImageSubscribers > 0)
                {
                    using (var frame = reference.ColorFrameReference.AcquireFrame())
                        image.LoadColorFrame(frame, _colorImageData, _colorImageBytes, CurrentColorWidth, MergeColorAndDepth);
                }

                using (var frame = reference.DepthFrameReference.AcquireFrame())
                    image.LoadDepthFrame(frame, _depthData, _depthImageSubscribers > 0, _depthImageBytes, CurrentDepthHeight);
                
                if (image.ColorImage != null)
                {
                    update(image.ColorImage.Value);
                    RaiseColorImageUpdated(image.ColorImage.Value);
                }

                if (_depthDataSubscribers > 0 && image.DepthData != null)
                    RaiseDepthDataReceived(image.DepthData);

                if (image.DepthImage != null)
                    RaiseDepthImageUpdated(image.DepthImage.Value);
            }
        }

        #region Frame Handling

        private void InitializeReader()
        {
            var types = FrameSourceTypes.None;

            if (_colorImageSubscribers > 0)
                types |= FrameSourceTypes.Color;

            if (_depthImageSubscribers + _depthDataSubscribers > 0)
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

        private void RaiseDepthDataReceived(ushort[] data)
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
                AddEvent(ref _depthImageUpdated, value, ref _depthImageSubscribers);
            }
            remove
            {
                RemoveEvent(ref _depthImageUpdated, value, ref _depthImageSubscribers);
            }
        }

        public event EventHandler<ushort[]> DepthDataReceived
        {
            add
            {
                AddEvent(ref _depthDataReceived, value, ref _depthDataSubscribers);
            }
            remove
            {
                RemoveEvent(ref _depthDataReceived, value, ref _depthDataSubscribers);
            }
        }

        #endregion
    }
}
