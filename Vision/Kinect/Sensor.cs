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

        public const int MergedColorFrameWidth = (int)((ColorFrameWidth / 707.0) * 513.0);

        public const int ColorFrameHeight = 1080;

        private readonly KinectSensor _sensor;

        private MultiSourceFrameReader _reader;

        private bool _mergeColorAndDepth;

        private readonly ushort[] _depthData;

        private ushort[] _depthDataCropped;

        private ushort[] _depthDataFlipped;

        private WriteableBitmap _depthImage;

        private readonly byte[] _colorData;

        private byte[] _colorDataCropped;

        private byte[] _colorDataFlipped;

        private WriteableBitmap _colorImage;

        private EventHandler<EventArgs> _colorImageUpdated;

        private int _colorImageSubscribers;

        private readonly Stopwatch _tickTimer;

        private long _colorTick;

        private long _depthTick;

        #endregion

        public Sensor()
            : base(new NyARIntSize((int)((ColorFrameWidth / 707.0) * 513.0), ColorFrameHeight))
        {
            _sensor = KinectSensor.GetDefault();

            GenerateDepthImage = true;
            MergeColorAndDepth = true;

            _depthData = new ushort[DepthFrameWidth * DepthFrameHeight];
            _depthDataCropped = new ushort[DepthFrameWidth * CurrentDepthHeight];
            _depthDataFlipped = new ushort[DepthFrameWidth * CurrentDepthHeight];
            _depthImage = BitmapFactory.New(DepthFrameWidth, CurrentDepthHeight);

            _colorData = new byte[ColorFrameWidth * ColorFrameHeight * 4];
            _colorDataCropped = new byte[CurrentColorWidth * ColorFrameHeight * 4];
            _colorDataFlipped = new byte[CurrentColorWidth * ColorFrameHeight * 4];
            _colorImage = BitmapFactory.New(CurrentColorWidth, ColorFrameHeight);

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

        public bool MergeColorAndDepth
        {
            get { return _mergeColorAndDepth; }
            set
            {
                if (_mergeColorAndDepth == value)
                    return;

                _mergeColorAndDepth = value;

                _depthDataCropped = new ushort[DepthFrameWidth * CurrentDepthHeight];
                _depthDataFlipped = new ushort[DepthFrameWidth * CurrentDepthHeight];
                _depthImage = BitmapFactory.New(DepthFrameWidth, CurrentDepthHeight);

                _colorDataCropped = new byte[CurrentColorWidth * ColorFrameHeight * 4];
                _colorDataFlipped = new byte[CurrentColorWidth * ColorFrameHeight * 4];
                _colorImage = BitmapFactory.New(CurrentColorWidth, ColorFrameHeight);
            }
        }

        public bool GenerateDepthImage { get; set; }

        public int CurrentColorWidth => MergeColorAndDepth ? MergedColorFrameWidth : ColorFrameWidth;

        // TODO: Allow to configure merge parameters
        // Merge Point 
        // Margin: 98,98,98,98. Depth: 513, 424.828125, Color: 707, 397.6875
        public int CurrentDepthHeight => MergeColorAndDepth ? 397 : DepthFrameHeight;

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

        internal async Task LoadDepthFrame(ushort[] depthData, ushort[] depthDataCropped, ushort[] depthDataFlipped, bool generateDepthImage, WriteableBitmap image, int depthImageHeight)
        {
            await Task.Run(() =>
            {
                depthData.CropImage(depthDataCropped, DepthFrameWidth, DepthFrameHeight, DepthFrameWidth, depthImageHeight);
                depthDataCropped.FlipImageHorizontally(depthDataFlipped, DepthFrameWidth);
            });

            if (DepthDataReceiver != null)
                await DepthDataReceiver(depthDataFlipped);

            if (generateDepthImage)
            {
                using (var context = image.GetBitmapContext(ReadWriteMode.ReadWrite))
                {
                    var pixelWidth = context.Width;

                    for (var i = 0; i < depthImageHeight; ++i)
                    {
                        for (var j = 0; j < DepthFrameWidth; ++j)
                        {
                            var depth = Math.Min(depthDataFlipped[i * DepthFrameWidth + j], MaxDepth);
                            var percent = (double)depth / MaxDepth;

                            var blueIntensity = percent > 0.5 ? (percent - 0.5) * 2 : 0;
                            var greenIntensity = percent > 0.5 ? 1 - percent : percent * 2;
                            var redIntensity = Math.Max(0, 1 - percent * 2);

                            unsafe
                            {
                                context.Pixels[i * pixelWidth + j] = -16777216 |
                                                                   (int)(depth == 0 ? 0 : 255 * redIntensity) << 16 |
                                                                   (int)(depth == 0 ? 0 : 255 * greenIntensity) << 8 |
                                                                   (int)(depth == 0 ? 0 : 255 * blueIntensity);
                            }
                        }
                    }
                }
            }
        }

        private async Task LoadColorFrame(byte[] pixels, byte[] pixelsCropped, byte[] pixelsFlipped, WriteableBitmap image, int currentWidth)
        {
            await Task.Run(() =>
            {
                pixels.CropImage(pixelsCropped, ColorFrameWidth, ColorFrameHeight, currentWidth, ColorFrameHeight, 4);
                pixelsCropped.FlipImageHorizontally(pixelsFlipped, currentWidth, 4);
            });

            // TODO: Allow to configure merge parameters
            // Merge Point 
            // Margin: 98,98,98,98. Depth: 513, 424.828125, Color: 707, 397.6875

            unsafe
            {
                using (var context = image.GetBitmapContext(ReadWriteMode.ReadWrite))
                {
                    var pixelWidth = context.Width;

                    for (var i = 0; i < ColorFrameHeight; ++i)
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

            await Task.Run(() =>
            {
                update(new Image
                {
                    ImageType = ImageType.Color,
                    Width = currentWidth,
                    Height = ColorFrameHeight,
                    DpiX = 96.0,
                    DpiY = 96.0,
                    Pixels = pixelsFlipped,
                    Stride = currentWidth * 4,
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

                await LoadDepthFrame(_depthData, _depthDataCropped, _depthDataFlipped, GenerateDepthImage, _depthImage, CurrentDepthHeight);

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

                await LoadColorFrame(_colorData, _colorDataCropped, _colorDataFlipped, _colorImage, CurrentColorWidth);

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
            add
            {
                AddEvent(ref _colorImageUpdated, value, ref _colorImageSubscribers);
            }
            remove
            {
                RemoveEvent(ref _colorImageUpdated, value, ref _colorImageSubscribers);
            }
        }

        #endregion
    }
}
