using System;
using System.Diagnostics;
using jp.nyatla.nyartoolkit.cs.core;

namespace Vision.Processing
{
    public struct Image : INyARRgbRaster, INyARRgbPixelDriver // TODO: Make immutable
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public double DpiX { get; set; }

        public double DpiY { get; set; }

        public byte[] Pixels { get; set; }

        public int Stride { get; set; }

        public byte BitsPerPixel { get; set; }

        #region INyARRgbRaster Implementation

        INyARRgbPixelDriver INyARRgbRaster.getRgbPixelDriver()
        {
            return this;
        }

        int INyARRaster.getWidth()
        {
            return Width;
        }

        int INyARRaster.getHeight()
        {
            return Height;
        }

        NyARIntSize INyARRaster.getSize()
        {
            return new NyARIntSize(Width, Height);
        }

        object INyARRaster.getBuffer()
        {
            return Pixels;
        }

        int INyARRaster.getBufferType()
        {
            if (BitsPerPixel == 32)
                return NyARBufferType.BYTE1D_B8G8R8X8_32;

            throw new NotImplementedException();
        }

        bool INyARRaster.isEqualBufferType(int i_type_value)
        {
            if (BitsPerPixel == 32)
                return NyARBufferType.BYTE1D_B8G8R8X8_32 == i_type_value;

            throw new NotImplementedException();
        }

        bool INyARRaster.hasBuffer()
        {
            return Pixels != null;
        }

        void INyARRaster.wrapBuffer(object i_ref_buf)
        {
            throw new NyARException();
        }

        object INyARRaster.createInterface(Type i_iid)
        {
            if (i_iid == typeof(INyARRgb2GsFilter))
                return NyARRgb2GsFilterFactory.createRgbAveDriver(this);

            if (i_iid == typeof (INyARPerspectiveCopy))
                return NyARPerspectiveCopyFactory.createDriver(this);

            throw new NotImplementedException();
        }

        #endregion

        #region INyARRgbPixelDriver Implementation

        NyARIntSize INyARRgbPixelDriver.getSize()
        {
            return new NyARIntSize(Width, Height);
        }

        void INyARRgbPixelDriver.getPixel(int i_x, int i_y, int[] i_rgb)
        {
            if (BitsPerPixel != 32)
                throw new NotImplementedException();

            var offset = ((i_y * Width) + i_x) * BitsPerPixel / 8;
            i_rgb[0] = Pixels[offset + 2];
            i_rgb[1] = Pixels[offset + 1];
            i_rgb[2] = Pixels[offset];
        }

        void INyARRgbPixelDriver.getPixelSet(int[] i_x, int[] i_y, int i_num, int[] i_intrgb)
        {
            if (BitsPerPixel != 32)
                throw new NotImplementedException();

            for (var i = 0; i < i_num; ++i)
            {
                var offset = ((i_y[i] * Width) + i_x[i]) * BitsPerPixel / 8;
                i_intrgb[i * 3] = Pixels[offset + 2];
                i_intrgb[i * 3 + 1] = Pixels[offset + 1];
                i_intrgb[i * 3 + 2] = Pixels[offset];
            }
        }

        void INyARRgbPixelDriver.setPixel(int i_x, int i_y, int i_r, int i_g, int i_b)
        {
            if (BitsPerPixel != 32)
                throw new NotImplementedException();

            Debug.Assert(i_r <= 255 && i_g <= 255 && i_b <= 255, "Overflow pixels");

            var offset = ((i_y * Width) + i_x) * BitsPerPixel / 8;
            Pixels[offset + 2] = (byte)i_r;
            Pixels[offset + 1] = (byte)i_g;
            Pixels[offset] = (byte)i_b;
        }

        void INyARRgbPixelDriver.setPixel(int i_x, int i_y, int[] i_rgb)
        {
            if (BitsPerPixel != 32)
                throw new NotImplementedException();

            var offset = ((i_y * Width) + i_x) * BitsPerPixel / 8;
            Pixels[offset + 2] = (byte)i_rgb[0];
            Pixels[offset + 1] = (byte)i_rgb[1];
            Pixels[offset] = (byte)i_rgb[2];
        }

        void INyARRgbPixelDriver.setPixels(int[] i_x, int[] i_y, int i_num, int[] i_intrgb)
        {
            if (BitsPerPixel != 32)
                throw new NotImplementedException();

            for (var i = 0; i < i_num; ++i)
            {
                var offset = ((i_y[i] * Width) + i_x[i]) * BitsPerPixel / 8;
                Pixels[offset + 2] = (byte)i_intrgb[i * 3];
                Pixels[offset + 1] = (byte)i_intrgb[i * 3 + 1];
                Pixels[offset] = (byte)i_intrgb[i * 3 + 2];
            }
        }

        void INyARRgbPixelDriver.switchRaster(INyARRgbRaster i_raster)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}