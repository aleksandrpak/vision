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
        private readonly Dictionary<DepthAngle, List<DepthData>> _map;

        private WriteableBitmap _bitmap;

        private double _currentAngle;

        private ManualResetEventSlim _servoEvent;

        private readonly Dictionary<int, int[]> _markers;
        private ushort _maxDepth;

        public Static2DMap(int width, int height, double horizontalAngle, double verticalAngle, ushort maxDepth)
        {
            Width = width;
            Height = height;
            HorizontalAngle = horizontalAngle;
            VerticalAngle = verticalAngle;
            MaxDepth = maxDepth;

            _bitmap = BitmapFactory.New(MaxDepth / 5, MaxDepth / 5 / 2);
            LastDepthData = new ushort[Width * Height];

            _map = new Dictionary<DepthAngle, List<DepthData>>();
            _markers = new Dictionary<int, int[]>();

            _currentAngle = 90;
        }

        public ImageSource Image => _bitmap;

        public ushort[] LastDepthData { get; }

        public int Width { get; }

        public int Height { get; }

        public double HorizontalAngle { get; }

        public double VerticalAngle { get; }

        public ushort MaxDepth
        {
            get { return _maxDepth; }
            set
            {
                _maxDepth = value;

                if (_bitmap == null)
                    return;

                var oldBitmap = _bitmap;
                lock (oldBitmap)
                {
                    _bitmap = BitmapFactory.New(value / 5, value / 5 / 2);
                }
            }
        }

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
            lock (_bitmap)
                _bitmap.Clear();
        }

        public void AddMarker(int id, int leftX, int leftY, int rightX, int rightY, int height)
        {
            lock (LastDepthData)
            {
                const double shift = Math.PI / 2.0;
                var mapWidth = (double)MaxDepth / 10;

                var leftDepth = FindDepth(leftX, leftY, 5);
                if (leftDepth == 0)
                    return;

                var rightDepth = FindDepth(rightX, rightY, 5);
                if (rightDepth == 0)
                    return;

                var currentRadians = _currentAngle * Math.PI / 180.0;

                var bottomLeftAngle = ((leftX - Width / 2) / (double)Width * HorizontalAngle * Math.PI / 180.0) - currentRadians + shift;
                var bottomLeftX = (int)(mapWidth + Math.Sin(bottomLeftAngle) * leftDepth);
                var bottomLeftY = (int)(mapWidth - Math.Cos(bottomLeftAngle) * leftDepth);

                var bottomRightAngle = ((rightX - Width / 2) / (double)Width * HorizontalAngle * Math.PI / 180.0) - currentRadians + shift;
                var bottomRightX = (int)(mapWidth + Math.Sin(bottomRightAngle) * rightDepth);
                var bottomRightY = (int)(mapWidth - Math.Cos(bottomRightAngle) * rightDepth);

                var topLeftX = bottomLeftX;
                var topLeftY = Math.Max(0, bottomLeftY - height);

                var topRightX = bottomRightX;
                var topRightY = Math.Max(0, bottomRightY - height);

                _markers[id] = new[] { bottomLeftX, bottomLeftY, bottomRightX, bottomRightY, topRightX, topRightY, topLeftX, topLeftY, bottomLeftX, bottomLeftY };
            }
        }

        public void RemoveMarker(int id)
        {
            lock (LastDepthData)
                _markers.Remove(id);
        }

        private ushort FindDepth(int x, int y, int steps)
        {
            for (var step = 0; step < steps; ++step)
            {
                for (var xStep = -step; xStep <= step; ++xStep)
                {
                    for (var yStep = -step; yStep <= step; ++yStep)
                    {
                        var newX = x + xStep;
                        var newY = y + yStep;

                        if (newX < 0 || newX > Width || newY < 0 || newY > Height)
                            continue;

                        var depth = LastDepthData[newY * Width + newX];
                        if (depth != 0)
                            return (ushort)(depth / 10);
                    }
                }
            }

            return 0;
        }

        public async Task Update(ushort[] data)
        {
            if (_servoEvent != null && _servoEvent.IsSet)
                return;

            await Task.Run(() => BuildMap(data));

            var width = (double)MaxDepth / 10;
            const double shift = Math.PI / 2.0;

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
                            var x = width + Math.Sin(angle) * depth;
                            var y = width - Math.Cos(angle) * depth;

                            if (x < 0 || x > pixelWidth || y < 0 || y > pixelWidth)
                                continue;

                            unsafe
                            {
                                context.Pixels[(int)y * pixelWidth + (int)x] = -16777216 | depthItem.Red << 16 | depthItem.Green << 8;
                            }
                        }
                    }
                }

                foreach (var marker in _markers.Values)
                    _bitmap.FillPolygon(marker, Colors.Blue);
            }

            _servoEvent?.Set();
        }

        private void BuildMap(ushort[] data)
        {
            lock (LastDepthData)
                Array.Copy(data, LastDepthData, Width * Height);

            var currentRadians = _currentAngle * Math.PI / 180.0;
            var maxDepth = MaxDepth;
            _map.Clear();

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
                    var depth = Math.Min(maxDepth, data[i * Width + (Width - j - 1)]);
                    if (depth == 0)
                        continue;

                    var verticalScreenAngle = Math.Abs((i - Height / 2) / (double)Height * VerticalAngle);
                    var height = (ushort)(Math.Sin(verticalScreenAngle * Math.PI / 180.0) * depth / Math.Sin((90 - verticalScreenAngle) * Math.PI / 180.0));

                    if (height > 2000)
                        continue;

                    var red = (byte)(((2000 - height) / 2000.0) * 255.0);
                    var green = (byte)(((height) / 2000.0) * 255.0);

                    depth = (ushort)(depth / Math.Sin((90 - horizontalScreenAngle) * Math.PI / 180.0));

                    depthData.Add(new DepthData(depth, red, green));
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
