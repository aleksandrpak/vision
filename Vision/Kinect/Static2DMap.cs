using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Kinect
{
    public sealed class Static2DMap
    {
        private readonly byte[] _map;

        private readonly int _center;

        private readonly int _width;

        private double _currentAngle;

        public Static2DMap(Sensor sensor)
        {
            _map = new byte[Sensor.MaxDepth * Sensor.MaxDepth * 4 / 8];

            _center = Sensor.MaxDepth / 8;
            _width = Sensor.MaxDepth * 2 / 8;

            _currentAngle = 0;

            sensor.DepthDataReceived += DepthDataReceivedEventHandler;
        }

        public void Rotate(double angle)
        {
            _currentAngle += angle;
        }

        private void DepthDataReceivedEventHandler(object sender, ushort[] data)
        {
            ClearMap();

            // TODO: Get map from range of depths
            var offset = Sensor.DepthFrameHeight / 2 * Sensor.DepthFrameWidth;

            for (var i = 0; i < Sensor.DepthFrameWidth; ++i)
            {
                var depth = Math.Min(Sensor.MaxDepth, data[i + offset]);
                if (depth == 0)
                    continue;

                var angle = (double)i / Sensor.DepthFrameWidth * Sensor.DepthFrameHorizontalAngle;
                angle -= 35;

                var x = (int)(depth * Math.Cos(angle));
                var y = (int)(depth * Math.Sin(angle));

                var byteCoordinate = ((Sensor.MaxDepth - y) * _width) + ((Sensor.MaxDepth + x) / 8);
                _map[byteCoordinate] |= (byte)(1 << (7 - (x % 8)));
            }

            RaiseMapImageUpdated();
        }

        private void ClearMap()
        {
            Array.Clear(_map, 0, _map.Length); // TODO: Clear using angle
        }

        private void RaiseMapImageUpdated()
        {
            MapImageUpdated?.Invoke(this, new Image
            {
                Width = Sensor.MaxDepth * 2,
                Height = Sensor.MaxDepth * 2,
                DpiX = 96.0,
                DpiY = 96.0,
                Pixels = _map,
                Stride = Sensor.MaxDepth * 2 / 8,
                BitsPerPixel = 1
            });
        }

        public event EventHandler<Image> MapImageUpdated;
    }
}
