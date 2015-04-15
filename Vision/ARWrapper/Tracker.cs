using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ARWrapper
{
    public sealed class Tracker
    {
        private readonly IntPtr _tracker;

        public Tracker(int width, int height, float markerWidth, int bytesPerPixel, PixelFormat pixelFormat, string cameraCalibrationFilename, string markersFilename)
        {
            _tracker = Methods.ARTKPConstructTrackerMulti(-1, width, height);
            if (_tracker == IntPtr.Zero)
                throw new Win32Exception("Failed to construct tracker");

            Methods.ARTKPInitMulti(_tracker, cameraCalibrationFilename, markersFilename, markerWidth * 0.01f, markerWidth * 100f);
            var result = Marshal.GetLastWin32Error();
            if (result != 0)
                throw new Win32Exception(result);

            if (Methods.ARTKPSetPixelFormat(_tracker, (int)pixelFormat) != (int)pixelFormat)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            Methods.ARTKPSetMarkerMode(_tracker, (int)MarkerMode.Simple);
            Methods.ARTKPSetBorderWidth(_tracker, 0.125f);

            if (!Methods.ARTKPIsAutoThresholdActivated(_tracker))
                Methods.ARTKPActivateAutoThreshold(_tracker, true);

            Methods.ARTKPSetUndistortionMode(_tracker, (int)UndistortionMode.Lut);
            Methods.ARTKPSetUseDetectLite(_tracker, false);

            Width = width;
            Height = height;
            BytesPerPixel = bytesPerPixel;

            Description = Marshal.PtrToStringAnsi(Methods.ARTKPGetDescription(_tracker));
        }

        public int Width { get; }

        public int Height { get; }

        public int BytesPerPixel { get; }

        public string Description { get; }

        public MarkerInfo[] Track(byte[] imageBytes)
        {
            var flipY = new byte[imageBytes.Length];
            for (var column = 0; column < Width; column++)
            {
                for (var row = 0; row < Height; row++)
                {
                    var sourcePixelOffset = GetPixelOffset(row, column, Width, BytesPerPixel);
                    var targetPixelOffset = GetPixelOffset(Height - row - 1, column, Width, BytesPerPixel);

                    for (var j = 0; j < BytesPerPixel; j++)
                        flipY[targetPixelOffset + j] = imageBytes[sourcePixelOffset + j];
                }
            }

            var markersCount = Methods.ARTKPCalcMulti(_tracker, flipY);

            var markers = new MarkerInfo[markersCount];
            for (var id = 0; id < markersCount; ++id)
                markers[id] = Methods.ARTKPGetDetectedMarkerStruct(_tracker, id);

            return markers;
        }

        private static int GetPixelOffset(int row, int column, int width, int bytesPerPixel)
        {
            return ((row * width) + column) * bytesPerPixel;
        }
    }
}
