using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Vision.Processing
{
    public sealed class Static2DMap
    {
        private readonly Dictionary<DepthAngle, List<DepthData>> _map;

        private WriteableBitmap _obstaclesBitmap;
        private WriteableBitmap _otherBitmap;

        private double _currentAngle;

        private readonly Dictionary<int, int[]> _markers;
        private ushort _maxDepth;

        public Static2DMap(int width, int height, double horizontalAngle, double verticalAngle, ushort maxDepth, ushort hostHeight)
        {
            Width = width;
            Height = height;
            HorizontalAngle = horizontalAngle;
            VerticalAngle = verticalAngle;
            MaxDepth = maxDepth;
            HostHeight = hostHeight;

            _obstaclesBitmap = BitmapFactory.New(MaxDepth / 5, MaxDepth / 5 / 2);
            _otherBitmap = BitmapFactory.New(MaxDepth / 5, MaxDepth / 5 / 2);
            LastDepthData = new ushort[Width * Height];

            _map = new Dictionary<DepthAngle, List<DepthData>>();
            _markers = new Dictionary<int, int[]>();

            _currentAngle = 90;
        }

        public ImageSource ObstaclesImage => _obstaclesBitmap;

        public ImageSource OtherImage => _otherBitmap;

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

                if (_obstaclesBitmap == null)
                    return;

                var oldBitmap = _obstaclesBitmap;
                lock (oldBitmap)
                {
                    _obstaclesBitmap = BitmapFactory.New(MaxDepth / 5, MaxDepth / 5 / 2);
                    _otherBitmap = BitmapFactory.New(MaxDepth / 5, MaxDepth / 5 / 2);
                }
            }
        }

        public ushort HostHeight { get; set; }

        public void SetAngle(double angle)
        {
            _currentAngle = angle;
        }

        public void Clear()
        {
            lock (_obstaclesBitmap)
            {
                _obstaclesBitmap.Clear();
                _otherBitmap.Clear();
            }
        }

        public void AddMarker(int id, Point topLeft, Point topRight, Point bottomLeft, Point bottomRight, int width, int depth)
        {
            lock (LastDepthData)
            {
                const double shift = Math.PI / 2.0;
                var maxDepth = MaxDepth;
                var mapWidth = (double)maxDepth / 10;

                var centerDepth = FindAverageDepth(Math.Max(topLeft.X, bottomLeft.X), Math.Max(topLeft.Y, topRight.Y), Math.Min(topRight.X, bottomRight.X), Math.Min(bottomLeft.Y, bottomRight.Y));
                if (centerDepth == 0)
                    return;

                var maxDiff = width / 2;

                var leftPoint = topLeft.X > bottomLeft.X ? topLeft : bottomLeft;
                var leftDepth = FindDepth(leftPoint.X, leftPoint.Y, 5, centerDepth - maxDiff, centerDepth + maxDiff);
                if (leftDepth == 0)
                    return;

                var rightPoint = topRight.X > bottomRight.X ? topRight : bottomRight;
                var rightDepth = FindDepth(rightPoint.X, rightPoint.Y, 5, centerDepth - maxDiff, centerDepth + maxDiff);
                if (rightDepth == 0)
                    return;

                var currentRadians = _currentAngle * Math.PI / 180.0;

                var bottomLeftAngle = ((leftPoint.X - Width / 2.0) / Width * HorizontalAngle * Math.PI / 180.0) - currentRadians + shift;
                var bottomLeftX = mapWidth + Math.Sin(bottomLeftAngle) * leftDepth;
                var bottomLeftY = mapWidth - Math.Cos(bottomLeftAngle) * leftDepth;

                var bottomRightAngle = ((rightPoint.X - Width / 2.0) / Width * HorizontalAngle * Math.PI / 180.0) - currentRadians + shift;
                var bottomRightX = mapWidth + Math.Sin(bottomRightAngle) * rightDepth;
                var bottomRightY = mapWidth - Math.Cos(bottomRightAngle) * rightDepth;

                var bottomLeftPoint = new Point(bottomLeftX, bottomLeftY);
                var angle = Math.Atan((bottomRightY - bottomLeftY) / (bottomRightX - bottomLeftX));
                var bottomRightPoint = ExtendLine(bottomLeftPoint, width, angle);
                var topLeftPoint = ExtendLine(bottomLeftPoint, -depth, angle + shift);
                var topRightPoint = ExtendLine(bottomRightPoint, -depth, angle + shift);

                _markers[id] = new[]
                {
                    (int)bottomLeftPoint.X, (int)bottomLeftPoint.Y,
                    (int)bottomRightPoint.X, (int)bottomRightPoint.Y,
                    (int)topRightPoint.X, (int)topRightPoint.Y,
                    (int)topLeftPoint.X, (int)topLeftPoint.Y,
                    (int)bottomLeftPoint.X, (int)bottomLeftPoint.Y
                };
            }
        }

        private static Point ExtendLine(Point first, double width, double angle)
        {
            var x3 = width * Math.Cos(angle);
            var y3 = width * Math.Sin(angle);

            return new Point(first.X + x3, first.Y + y3);
        }

        public void RemoveMarker(int id)
        {
            lock (LastDepthData)
                _markers.Remove(id);
        }

        private ushort FindAverageDepth(double x1, double y1, double x2, double y2)
        {
            double sum = 0;
            double count = 0;

            for (var i = Math.Min(y1, y2); i <= Math.Max(y1, y2); ++i)
            {
                for (var j = Math.Min(x1, x2); j <= Math.Max(x1, x2); ++j)
                {
                    var depth = LastDepthData[(int)i * Width + (Width - (int)j)];
                    if (depth == 0)
                        continue;

                    sum += depth;
                    ++count;
                }
            }

            return (ushort)(sum / count / 10.0);
        }

        private ushort FindDepth(double x, double y, int steps, int min, int max)
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

                        var depth = LastDepthData[(int)newY * Width + (Width - (int)newX)] / 10;
                        if (depth == 0)
                            continue;

                        if (depth < min || depth > max)
                            continue;

                        return (ushort)depth;
                    }
                }
            }

            return 0;
        }

        public async Task Update(ushort[] data)
        {
            await Task.Run(() => BuildMap(data));

            var width = (double)MaxDepth / 10;
            const double shift = Math.PI / 2.0;

            lock (_obstaclesBitmap)
            {
                using (var obstaclesContext = _obstaclesBitmap.GetBitmapContext(ReadWriteMode.ReadWrite))
                using (var otherContext = _otherBitmap.GetBitmapContext(ReadWriteMode.ReadWrite))
                {
                    obstaclesContext.Clear();
                    otherContext.Clear();

                    var pixelWidth = obstaclesContext.Width;

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
                                if (depthItem.IsObstacle)
                                    obstaclesContext.Pixels[(int)y * pixelWidth + (int)x] = -16777216 | 255 << 16;
                                else
                                    otherContext.Pixels[(int)y * pixelWidth + (int)x] = -16777216 | 255 << 8;
                            }
                        }
                    }
                }

                foreach (var marker in _markers.Values)
                    _obstaclesBitmap.FillPolygon(marker, Colors.Blue);
            }
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

                    var isObstacle = false;

                    if (i >= Height / 2)
                    {
                        var verticalScreenAngle = Math.Abs((i - Height / 2) / (double)Height * VerticalAngle);
                        var height = (ushort)(Math.Sin(verticalScreenAngle * Math.PI / 180.0) * depth / Math.Sin((90 - verticalScreenAngle) * Math.PI / 180.0));

                        if (height <= HostHeight * 10)
                            isObstacle = true;
                    }

                    depth = (ushort)(depth / Math.Sin((90 - horizontalScreenAngle) * Math.PI / 180.0));

                    depthData.Add(new DepthData(depth, isObstacle));
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
            public DepthData(ushort depth, bool isObstacle)
            {
                Depth = depth;
                IsObstacle = isObstacle;
            }

            public ushort Depth { get; }
            public bool IsObstacle { get; }
        }
    }
}
