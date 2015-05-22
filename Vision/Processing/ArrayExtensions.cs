using System;

namespace Vision.Processing
{
    public static class ArrayExtensions
    {
        public static void CropImage<T>(this T[] arr1, T[] arr2, int oldWidth, int oldHeight, int newWidth, int newHeight, int bitsPerPixel = 1)
        {
            if (oldWidth == newWidth && oldHeight == newHeight)
                return;

            var verticalDiff = (oldHeight - newHeight) / 2;
            var horizontalDiff = (oldWidth - newWidth) / 2;

            for (var i = verticalDiff; i < newHeight; ++i)
                Array.Copy(arr1, i * oldWidth * bitsPerPixel + horizontalDiff * bitsPerPixel, arr2, (i - verticalDiff) * newWidth * bitsPerPixel, newWidth * bitsPerPixel);
        }

        public static void FlipImageHorizontally<T>(this T[] arr1, T[] arr2, int width, int bitsPerPixel = 1)
        {
            for (var i = 0; i < arr1.Length / width / bitsPerPixel; ++i)
            {
                for (var j = 0; j < width; ++j)
                {
                    if (bitsPerPixel == 1)
                    {
                        arr2[i * width + j] = arr1[i * width + width - j - 1];
                    }
                    else if (bitsPerPixel == 4)
                    {
                        var sourceOffset = i * width * 4 + (width - j - 1) * 4;
                        var destinationOffset = i * width * 4 + j * 4;

                        arr2[destinationOffset] = arr1[sourceOffset];
                        arr2[destinationOffset + 1] = arr1[sourceOffset + 1];
                        arr2[destinationOffset + 2] = arr1[sourceOffset + 2];
                        arr2[destinationOffset + 3] = arr1[sourceOffset + 3];
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException($"{nameof(bitsPerPixel)} should be 1 or 4");
                    }
                }
            }
        }
    }
}
