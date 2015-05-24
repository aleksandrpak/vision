using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Vision.Processing
{
    public sealed class Static2DMap
    {
        private readonly List<DepthData>[] _map;

        private readonly WriteableBitmap _bitmap;

        private double _currentAngle;

        private ManualResetEventSlim _servoEvent;

        public Static2DMap(int width, int height, double horizontalAngle, double verticalAngle, ushort maxDepth)
        {
            Width = width;
            Height = height;
            HorizontalAngle = horizontalAngle;
            VerticalAngle = verticalAngle;
            MaxDepth = maxDepth;

            _bitmap = BitmapFactory.New(maxDepth / 5, maxDepth / 5);

            _map = new List<DepthData>[(int)(360.0 / (HorizontalAngle / Width))];

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

        public void Update(ushort[] data)
        {
            if (_servoEvent != null && _servoEvent.IsSet)
                return;

            for (var j = 0; j < Width; ++j)
            {
                var angle = NormalizeAngle(_currentAngle - 90 - ((double)j / Width * HorizontalAngle));
                var index = (int)(angle / 360.0 * (_map.Length - 1));
                var horizontalScreenAngle = (j - Width / 2) / (double)Width * HorizontalAngle;

                if (_map[index] == null)
                    _map[index] = new List<DepthData>();
                else
                    _map[index].Clear();

                for (var i = 0; i < Height; ++i)
                {
                    var depth = Math.Min(MaxDepth, data[i * Width + j]);
                    if (depth == 0)
                        continue;

                    var verticalScreenAngle = Math.Abs((i - Height / 2) / (double)Height * VerticalAngle);
                    var height = (ushort)(Math.Sin((verticalScreenAngle) * Math.PI / 180.0) * depth / Math.Sin((90 - verticalScreenAngle) * Math.PI / 180.0));

                    if (height > 2000)
                        continue;

                    var red = (byte)(((2000 - height) / 2000.0) * 255.0);
                    var green = (byte)(((height) / 2000.0) * 255.0);

                    depth = (ushort)(depth / Math.Sin((90 - horizontalScreenAngle) * Math.PI / 180.0));

                    _map[index].Add(new DepthData(depth, green, red));
                }
            }

            var width = (double)MaxDepth * 2 / 10;

            unsafe
            {
                using (var context = _bitmap.GetBitmapContext(ReadWriteMode.ReadWrite))
                {
                    context.Clear();
                    var pixelWidth = context.Width;

                    for (var i = 0.0; i < _map.Length; ++i)
                    {
                        var depths = _map[(int)i];
                        if (depths == null)
                            continue;

                        for (var j = 0; j < depths.Count; ++j)
                        {
                            var item = _map[(int)i][j];
                            var depth = item.Depth / 10;
                            if (depth == 0)
                                continue;

                            var angle = i / _map.Length * 2 * Math.PI + 2.181661565; // + 125 degrees
                            var x = (int)((width / 2) + Math.Cos(angle) * depth);
                            var y = (int)((width / 2) - Math.Sin(angle) * depth);

                            context.Pixels[y * pixelWidth + x] = -16777216 | item.Red << 16 | item.Green << 8;
                        }
                    }
                }
            }

            _servoEvent?.Set();
        }

        private static double NormalizeAngle(double angle)
        {
            while (angle < 0 || angle > 360)
                angle += angle < 0 ? 360 : -360;

            return angle;
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
