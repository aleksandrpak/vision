using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

        public const double DepthFrameVerticalAngle = 60;

        public const int DepthFrameWidth = 512;

        public const int DepthFrameHeight = 424;

        public const int ColorFrameWidth = 1920;

        public const int ColorFrameHeight = 1080;

        private readonly KinectSensor _sensor;

        private MultiSourceFrameReader _reader;

        private readonly ushort[] _depthData;

        private readonly WriteableBitmap _depthImage;

        private readonly byte[] _colorData;

        private readonly byte[] _colorDataFlipped;

        private readonly WriteableBitmap _colorImage;

        private EventHandler<EventArgs> _colorImageUpdated;

        private int _colorImageSubscribers;

        private readonly Stopwatch _tickTimer;

        private long _colorTick;

        private long _depthTick;

        private readonly DepthSpacePoint[] _lastDepthPoints;

        #endregion

        public Sensor()
            : base(new NyARIntSize(ColorFrameWidth, ColorFrameHeight))
        {
            _sensor = KinectSensor.GetDefault();

            GenerateDepthImage = true;

            _depthData = new ushort[DepthFrameWidth * DepthFrameHeight];
            _depthImage = BitmapFactory.New(ColorFrameWidth, ColorFrameHeight);

            _colorData = new byte[ColorFrameWidth * ColorFrameHeight * 4];
            _colorDataFlipped = new byte[ColorFrameWidth * ColorFrameHeight * 4];
            _colorImage = BitmapFactory.New(ColorFrameWidth, ColorFrameHeight);

            _lastDepthPoints = new DepthSpacePoint[ColorFrameWidth * ColorFrameHeight];

            _tickTimer = Stopwatch.StartNew();

            if (_sensor == null)
                throw new InvalidOperationException("Failed to initialize Kinect sensor.");

            _sensor.Open();

            Thread.Sleep(1000); // Let it initialize

            if (!_sensor.IsOpen || !_sensor.IsAvailable)
                throw new InvalidOperationException("Failed to open Kinect sensor.");
        }

        public bool IsConnected => _sensor.IsOpen && _sensor.IsAvailable;
        public ImageSource DepthImage => _depthImage;
        public ImageSource ColorImage => _colorImage;
        public Func<ushort[], Task> DepthDataReceiver { get; set; }
        public bool GenerateDepthImage { get; set; }

        public void GetDepthSpacePoint(ref int x, ref int y)
        {
            var point = _lastDepthPoints[y * ColorFrameWidth + x];
            x = (int)point.X;
            y = (int)point.Y;
        }

        private async void MultiSourceFrameArrivedEventHandler(object sender, MultiSourceFrameArrivedEventArgs args)
        {
            var reference = args.FrameReference.AcquireFrame();
            var currentTick = _tickTimer.ElapsedMilliseconds / 100;

            await AcquireColorFrame(currentTick, reference);
            await AcquireDepthFrame(currentTick, reference);
        }

        #region Frame Handling

        private void InitializeReader()
        {
            var types = FrameSourceTypes.None;

            if (_colorImageSubscribers > 0)
                types |= FrameSourceTypes.Color;

            if (GenerateDepthImage || DepthDataReceiver != null)
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

        private async Task LoadDepthFrame(ushort[] depthData, bool generateDepthImage, WriteableBitmap image)
        {
            await Task.Run(() =>
            {
                _sensor.CoordinateMapper.MapColorFrameToDepthSpace(depthData, _lastDepthPoints);
            });

            if (DepthDataReceiver != null)
                await DepthDataReceiver(depthData);

            if (!generateDepthImage)
                return;

            using (var context = image.GetBitmapContext(ReadWriteMode.ReadWrite))
            {
                context.Clear();

                for (var i = 0; i < ColorFrameHeight; ++i)
                {
                    for (var j = 0; j < ColorFrameWidth; ++j)
                    {
                        var depthPoint = _lastDepthPoints[i*ColorFrameWidth + j];
                        if (float.IsNegativeInfinity(depthPoint.X) || float.IsNegativeInfinity(depthPoint.Y))
                            continue;

                        var depth = Math.Min(depthData[(int)depthPoint.Y * DepthFrameWidth + (int)depthPoint.X], MaxDepth);
                        var percent = (double)depth / MaxDepth;

                        var blueIntensity = percent > 0.5 ? (percent - 0.5) * 2 : 0;
                        var greenIntensity = percent > 0.5 ? 1 - percent : percent * 2;
                        var redIntensity = Math.Max(0, 1 - percent * 2);
                        var pixel = -16777216 |
                                    (int)(depth == 0 ? 0 : 255 * redIntensity) << 16 |
                                    (int)(depth == 0 ? 0 : 255 * greenIntensity) << 8 |
                                    (int)(depth == 0 ? 0 : 255 * blueIntensity);

                        unsafe
                        {
                            context.Pixels[i * ColorFrameWidth + (ColorFrameWidth - j - 1)] = pixel;
                        }
                    }
                }
            }
        }

        private async Task LoadColorFrame(byte[] pixels, byte[] pixelsFlipped, WriteableBitmap image)
        {
            await Task.Run(() =>
            {
                pixels.FlipImageHorizontally(pixelsFlipped, ColorFrameWidth, 4);
            });

            unsafe
            {
                using (var context = image.GetBitmapContext(ReadWriteMode.ReadWrite))
                {
                    var pixelWidth = context.Width;

                    for (var i = 0; i < ColorFrameHeight; ++i)
                    {
                        for (var j = 0; j < ColorFrameWidth; ++j)
                        {
                            var offset = i * ColorFrameWidth * 4 + j * 4;

                            context.Pixels[i * pixelWidth + j] = -16777216 |
                                                               pixelsFlipped[offset + 2] << 16 |
                                                               pixelsFlipped[offset + 1] << 8 |
                                                               pixelsFlipped[offset];
                        }
                    }
                }
            }

            await Task.Run(() =>
            {
                update(new Image
                {
                    ImageType = ImageType.Color,
                    Width = ColorFrameWidth,
                    Height = ColorFrameHeight,
                    DpiX = 96.0,
                    DpiY = 96.0,
                    Pixels = pixelsFlipped,
                    Stride = ColorFrameWidth * 4,
                    BitsPerPixel = 32
                });
            });

            RaiseColorImageUpdated();
        }

        private async Task AcquireDepthFrame(long currentTick, MultiSourceFrame reference)
        {
            if (_depthTick == currentTick || (DepthDataReceiver == null && !GenerateDepthImage))
                return;

            using (var frame = reference.DepthFrameReference.AcquireFrame())
            {
                if (frame == null)
                    return;

                _depthTick = currentTick;

                if (!Monitor.TryEnter(_depthData))
                    return;

                frame.CopyFrameDataToArray(_depthData);

                await LoadDepthFrame(_depthData, GenerateDepthImage, _depthImage);

                Monitor.Exit(_depthData);
            }
        }

        private async Task AcquireColorFrame(long currentTick, MultiSourceFrame reference)
        {
            if (_colorImageSubscribers <= 0 || _colorTick == currentTick)
                return;

            using (var frame = reference.ColorFrameReference.AcquireFrame())
            {
                if (frame == null)
                    return;

                _colorTick = currentTick;

                if (!Monitor.TryEnter(_colorData))
                    return;

                if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
                    frame.CopyRawFrameDataToArray(_colorData);
                else
                    frame.CopyConvertedFrameDataToArray(_colorData, ColorImageFormat.Bgra);

                await LoadColorFrame(_colorData, _colorDataFlipped, _colorImage);

                Monitor.Exit(_colorData);
            }
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

        private void RaiseColorImageUpdated()
        {
            _colorImageUpdated?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler<EventArgs> ColorImageUpdated
        {
            add { AddEvent(ref _colorImageUpdated, value, ref _colorImageSubscribers); }
            remove { RemoveEvent(ref _colorImageUpdated, value, ref _colorImageSubscribers); }
        }

        #endregion
    }
}