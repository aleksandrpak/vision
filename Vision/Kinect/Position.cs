namespace Vision.Kinect
{
    public struct Position
    {
        public Position(int x, int y, int width, int height)
            : this()
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; } 

        public int Y { get; }

        public int Width { get; }

        public int Height { get; }
    }
}