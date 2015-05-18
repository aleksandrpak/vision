using System;
using System.Collections.Generic;

namespace Vision.Processing
{
    public sealed class Static2DMap
    {
        private readonly ushort[] _map;

        private double _currentAngle;

        public Static2DMap(int width, int height, double horizontalAngle, ushort maxDepth)
        {
            Width = width;
            Height = height;
            HorizontalAngle = horizontalAngle;
            MaxDepth = maxDepth;

            _map = new ushort[(int)(360.0 / (HorizontalAngle / Width))];

            _currentAngle = 0;
        }

        public void Rotate(double angle)
        {
            _currentAngle += angle;
        }

        public int Width { get; }

        public int Height { get; }

        public double HorizontalAngle { get; }

        public ushort MaxDepth { get; }

        public void Update(ushort[] data)
        {
            for (var j = 0; j < Width; ++j)
            {
                var minDepth = 0;
                for (var i = Height / 2 - 50; i < Height / 2 + 50; ++i) // TODO: Use height of robot
                {
                    var depth = -Math.Min(MaxDepth, data[i * Width + j]);
                    if (depth < minDepth)
                        minDepth = depth;
                }

                var angle = NormalizeAngle(_currentAngle - ((double)j / Width * HorizontalAngle));
                var screenAngle = (j - Width / 2) / (double)Width * 70.0;

                minDepth = -minDepth;
                minDepth = (int)(minDepth / Math.Sin((90 - screenAngle) * Math.PI / 180.0));

                _map[(int)(angle / 360.0 * (_map.Length - 1))] = (ushort)minDepth;
            }

            if (MapImageUpdated == null)
                return;

            var width = (double)MaxDepth * 2 / 10;
            var pixels = new byte[(int)width * (int)width];

            for (var i = 0.0; i < _map.Length; ++i)
            {
                var depth = _map[(int)i] / 10;
                if (depth == 0)
                    continue;

                var angle = i / _map.Length * 2 * Math.PI + 2.181661565; // + 125 degrees
                var x = (int)((width / 2) + Math.Cos(angle) * depth);
                var y = (int)((width / 2) - Math.Sin(angle) * depth);

                SetPixels(pixels, (int)width, x, y);
            }

            RaiseMapImageUpdated(pixels, (int)width, (int)width);
        }

        private static void SetPixels(IList<byte> pixels, int width, int x, int y)
        {
            if (y - 1 > 0)
                SetPixelsRow(pixels, width, x, y - 1);

            SetPixelsRow(pixels, width, x, y);

            if ((y + 1) * width < pixels.Count)
                SetPixelsRow(pixels, width, x, y + 1);
        }

        private static void SetPixelsRow(IList<byte> pixels, int width, int x, int y)
        {
            var firstPixel = y * width + (x - 1);
            if (firstPixel > 0)
                pixels[firstPixel] = 255;

            pixels[y * width + x] = 255;

            var thirdPixel = y * width + (x + 1);
            if (thirdPixel < pixels.Count)
                pixels[thirdPixel] = 255;
        }

        private static double NormalizeAngle(double angle)
        {
            while (angle < 0 || angle > 360)
                angle += angle < 0 ? 360 : -360;

            return angle;
        }

        private void RaiseMapImageUpdated(byte[] pixels, int width, int height)
        {
            MapImageUpdated?.Invoke(this, new Image
            {
                ImageType = ImageType.Map,
                Width = width,
                Height = height,
                DpiX = 96.0,
                DpiY = 96.0,
                Pixels = pixels,
                Stride = width,
                BitsPerPixel = 8
            });
        }

        public event EventHandler<Image> MapImageUpdated;
    }
}
