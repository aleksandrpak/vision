using System;
using System.Runtime.InteropServices;

namespace ARWrapper
{
    public static class Methods
    {
        [DllImport("ARToolKitPlus.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern IntPtr ARTKPConstructTrackerMulti(int trackerSwitch, int imageWidth, int imageHeight);

        [DllImport("ARToolKitPlus.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern IntPtr ARTKPGetDescription(IntPtr tracker);

        [DllImport("ARToolKitPlus.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern int ARTKPSetPixelFormat(IntPtr tracker, int pixelFormat);

        [DllImport("ARToolKitPlus.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern int ARTKPInitMulti(IntPtr tracker, string cameraCalibrationFilename, string markersFilename, float nearClip, float farClip);

        [DllImport("ARToolKitPlus.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern void ARTKPSetMarkerMode(IntPtr tracker, int markerMode);

        [DllImport("ARToolKitPlus.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern void ARTKPSetBorderWidth(IntPtr tracker, float value);

        [DllImport("ARToolKitPlus.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern bool ARTKPIsAutoThresholdActivated(IntPtr tracker);

        [DllImport("ARToolKitPlus.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern void ARTKPActivateAutoThreshold(IntPtr tracker, bool enable);

        [DllImport("ARToolKitPlus.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern void ARTKPSetUndistortionMode(IntPtr tracker, int undistortionMode);

        [DllImport("ARToolKitPlus.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern void ARTKPSetUseDetectLite(IntPtr trackerMulti, bool enable);

        [DllImport("ARToolKitPlus.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern int ARTKPCalcMulti(IntPtr trackerMulti, byte[] cameraBuffer);

        [DllImport("ARToolKitPlus.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern NativeMarkerInfo ARTKPGetDetectedMarkerStruct(IntPtr trackerMulti, int marker);
    }
}
