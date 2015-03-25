namespace Vision.Kinect
{
    public struct Image // TODO: Make immutable
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public double DpiX { get; set; }

        public double DpiY { get; set; }

        public byte[] Pixels { get; set; }

        public int Stride { get; set; }
    }
}