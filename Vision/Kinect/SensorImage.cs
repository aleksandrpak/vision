using System;
using System.Threading;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using Vision.Processing;

namespace Vision.Kinect
{
    public struct SensorImage : IDisposable
    {
        private readonly object _lockObject;

        public SensorImage(object lockObject)
            : this()
        {
            _lockObject = lockObject;
            IsLocked = Monitor.TryEnter(_lockObject);
        }

        public bool IsLocked { get; }

        public ushort[] DepthData { get; private set; }

        public Image? ColorImage { get; private set; }

        

        internal void LoadColorFrame(ColorFrame frame, byte[] pixels, byte[] pixelsCropped, byte[] pixelsFlipped, WriteableBitmap image, int currentWidth)
        {
            if (frame == null)
                return;

            
        }

        void IDisposable.Dispose()
        {
            if (IsLocked)
                Monitor.Exit(_lockObject);
        }
    }
}