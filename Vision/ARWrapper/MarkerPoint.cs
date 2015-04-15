namespace ARWrapper
{
    public struct MarkerPoint
    {
        public MarkerPoint(float x, float y)
            : this()
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        
        public float Y { get; } 
    }
}