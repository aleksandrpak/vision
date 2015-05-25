using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Vision.Processing
{
    public sealed class Static2DMap
    {
        private readonly int _moveRadius;

        private readonly Dictionary<DepthAngle, List<DepthData>> _map;

        private readonly WriteableBitmap _bitmap;

        private double _currentAngle;

        private ManualResetEventSlim _servoEvent;

        public Static2DMap(int width, int height, double horizontalAngle, double verticalAngle, ushort maxDepth, int moveRadius)
        {
            _moveRadius = moveRadius;
            Width = width;
            Height = height;
            HorizontalAngle = horizontalAngle;
            VerticalAngle = verticalAngle;
            MaxDepth = maxDepth;

            _bitmap = BitmapFactory.New(maxDepth / 5, maxDepth / 5);

            _map = new Dictionary<DepthAngle, List<DepthData>>();

            _currentAngle = 90;
        }

        public ImageSource Image => _bitmap;

        public int Width { get; }

        public int Height { get; }

        public double HorizontalAngle { get; }

        public double VerticalAngle { get; }

        public ushort MaxDepth { get; }

        public void SetAngle(double angle)
        {
            _currentAngle = angle;
        }

        public void ConnectServo(ManualResetEventSlim servoEvent)
        {
            _servoEvent = servoEvent;
        }

        public void DisconnectServo()
        {
            _servoEvent = null;
        }

        public void Clear()
        {
            _bitmap.Clear();
        }

        public async Task Update(ushort[] data)
        {
            if (_servoEvent != null && _servoEvent.IsSet)
                return;

            await Task.Run(() => BuildMap(data));

            var width = (double)MaxDepth * 2 / 10;
            var shift = Math.PI / 2.0;

            lock (_bitmap)
            {
                using (var context = _bitmap.GetBitmapContext(ReadWriteMode.ReadWrite))
                {
                    context.Clear();
                    var pixelWidth = context.Width;

                    foreach (var depthPair in _map)
                    {
                        var depthData = depthPair.Value;
                        if (depthData == null)
                            continue;

                        var depthAngle = depthPair.Key;
                        foreach (var depthItem in depthData)
                        {
                            var depth = depthItem.Depth / 10;
                            if (depth == 0)
                                continue;

                            var angle = depthAngle.ScreenAngle - depthAngle.ViewAngle + shift;
                            var x = (width / 2) + Math.Sin(angle) * depth;
                            var y = (width / 2) - Math.Cos(angle) * depth;

                            var viewAngle = -depthAngle.ViewAngle + shift;
                            x += Math.Sin(viewAngle) * _moveRadius;
                            y += Math.Cos(viewAngle) * _moveRadius;

                            if (x < 0 || x > pixelWidth || y < 0 || y > pixelWidth)
                                continue;

                            unsafe
                            {
                                context.Pixels[(int)y * pixelWidth + (int)x] = -16777216 | depthItem.Red << 16 | depthItem.Green << 8;
                            }
                        }
                    }
                }
            }

            _servoEvent?.Set();
        }

        private void BuildMap(IReadOnlyList<ushort> data)
        {
            var currentRadians = _currentAngle * Math.PI / 180.0;

            for (var j = 0; j < Width; ++j)
            {
                var horizontalScreenAngle = (j - Width / 2) / (double)Width * HorizontalAngle;
                var depthAngle = new DepthAngle(currentRadians, horizontalScreenAngle * Math.PI / 180.0);

                List<DepthData> depthData;
                if (!_map.TryGetValue(depthAngle, out depthData))
                    _map[depthAngle] = depthData = new List<DepthData>();

                depthData.Clear();

                for (var i = 0; i < Height; ++i)
                {
                    var depth = Math.Min(MaxDepth, data[i * Width + j]);
                    if (depth == 0)
                        continue;

                    var verticalScreenAngle = Math.Abs((i - Height / 2) / (double)Height * VerticalAngle);
                    var height = (ushort)(Math.Sin(verticalScreenAngle * Math.PI / 180.0) * depth / Math.Sin((90 - verticalScreenAngle) * Math.PI / 180.0));

                    if (height > 2000)
                        continue;

                    var red = (byte)(((2000 - height) / 2000.0) * 255.0);
                    var green = (byte)(((height) / 2000.0) * 255.0);

                    depth = (ushort)(depth / Math.Sin((90 - horizontalScreenAngle) * Math.PI / 180.0));

                    depthData.Add(new DepthData(depth, green, red));
                }
            }
        }

        private struct DepthAngle : IEquatable<DepthAngle>
        {
            public DepthAngle(double viewAngle, double screenAngle)
            {
                ViewAngle = viewAngle;
                ScreenAngle = screenAngle;
            }

            public double ViewAngle { get; }
            public double ScreenAngle { get; }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                    return false;

                return obj is DepthAngle && Equals((DepthAngle)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (ViewAngle.GetHashCode() * 397) ^ ScreenAngle.GetHashCode();
                }
            }

            public bool Equals(DepthAngle other)
            {
                return ViewAngle.Equals(other.ViewAngle) && ScreenAngle.Equals(other.ScreenAngle);
            }
        }

        private struct DepthData
        {
            public DepthData(ushort depth, byte red, byte green)
            {
                Depth = depth;
                Red = red;
                Green = green;
            }

            public ushort Depth { get; }
            public byte Red { get; }
            public byte Green { get; }
        }
    }
}
