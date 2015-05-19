using System;
using System.Collections.Generic;

namespace Vision.Processing
{
    public sealed class Static2DMap
    {
        private readonly List<Tuple<ushort, byte, byte>>[] _map;

        private double _currentAngle;

        public Static2DMap(int width, int height, double horizontalAngle, ushort maxDepth)
        {
            Width = width;
            Height = height;
            HorizontalAngle = horizontalAngle;
            MaxDepth = maxDepth;

            _map = new List<Tuple<ushort, byte, byte>>[(int)(360.0 / (HorizontalAngle / Width))];

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
                var angle = NormalizeAngle(_currentAngle - ((double)j / Width * HorizontalAngle));
                var index = (int)(angle / 360.0 * (_map.Length - 1));
                var horizontalScreenAngle = (j - Width / 2) / (double)Width * 70.0;

                if (_map[index] == null)
                    _map[index] = new List<Tuple<ushort, byte, byte>>();
                else
                    _map[index].Clear();

                for (var i = 0; i < Height; ++i)
                {
                    var depth = Math.Min(MaxDepth, data[i * Width + j]);
                    if (depth == 0)
                        continue;

                    var verticalScreenAngle = Math.Abs((i - Height / 2) / (double)Height * 60.0);
                    var height = (ushort)(Math.Sin((verticalScreenAngle) * Math.PI / 180.0) * depth / Math.Sin((90 - verticalScreenAngle) * Math.PI / 180.0));

                    if (height > 2000)
                        continue;

                    var red = (byte)(((2000 - height) / 2000.0) * 255.0);
                    var green = (byte)(((height) / 2000.0) * 255.0);

                    depth = (ushort)(depth / Math.Sin((90 - horizontalScreenAngle) * Math.PI / 180.0));

                    _map[index].Add(new Tuple<ushort, byte, byte>(depth, green, red));
                }
            }

            if (MapImageUpdated == null)
                return;

            var width = (double)MaxDepth * 2 / 10;
            var pixels = new byte[(int)width * (int)width * 3];

            for (var i = 0.0; i < _map.Length; ++i)
            {
                var depths = _map[(int)i];
                if (depths == null)
                    continue;

                for (var j = 0; j < depths.Count; ++j)
                {
                    var item = _map[(int)i][j];
                    var depth = item.Item1 / 10;
                    if (depth == 0)
                        continue;

                    var angle = i / _map.Length * 2 * Math.PI + 2.181661565; // + 125 degrees
                    var x = (int)((width / 2) + Math.Cos(angle) * depth);
                    var y = (int)((width / 2) - Math.Sin(angle) * depth);

                    SetPixels(pixels, (int)width, x, y, item.Item2, item.Item3);
                }
            }

            RaiseMapImageUpdated(pixels, (int)width, (int)width);
        }

        private static void SetPixels(IList<byte> pixels, int width, int x, int y, byte green, byte red)
        {
            //if (y - 1 > 0)
            //    SetPixelsRow(pixels, width, x, y - 1, green, red);

            SetPixelsRow(pixels, width, x, y, green, red);

            //if ((y + 1) < width)
            //    SetPixelsRow(pixels, width, x, y + 1, green, red);
        }

        private static void SetPixelsRow(IList<byte> pixels, int width, int x, int y, byte green, byte red)
        {
            //var firstPixel = y * width * 3 + ((x - 1) * 3);
            //if (firstPixel > 0)
            //{
            //    pixels[firstPixel] = 0;
            //    pixels[firstPixel + 1] = green;
            //    pixels[firstPixel + 2] = red;
            //}

            var secondPixel = y * width * 3 + (x * 3);
            pixels[secondPixel] = 0;
            pixels[secondPixel + 1] = green;
            pixels[secondPixel + 2] = red;

            //var thirdPixel = y * width * 3 + ((x + 1) * 3);
            //if (thirdPixel < pixels.Count)
            //{
            //    pixels[thirdPixel] = 0;
            //    pixels[thirdPixel + 1] = green;
            //    pixels[thirdPixel + 2] = red;
            //}
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
                Stride = width * 3,
                BitsPerPixel = 24
            });
        }

        public event EventHandler<Image> MapImageUpdated;
    }
}
