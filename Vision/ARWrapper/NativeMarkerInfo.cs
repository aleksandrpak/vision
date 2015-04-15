namespace ARWrapper
{
    public unsafe struct NativeMarkerInfo
    {
        /// <summary>
        /// The number of pixels in the labeled region.
        /// </summary>
        public int area;

        /// <summary>
        /// The marker identitied number.
        /// </summary>
        public int id;

        /// <summary>
        /// Direction that tells about the rotation about the marker (possible values are 0, 1, 2 or 3). 
        /// This parameter makes it possible to tell about the line order of the detected marker 
        /// (so which line is the first one) and so find the first vertex. 
        /// This is important to compute the transformation matrix in arGetTransMat(). 
        /// </summary>
        public int dir;

        /// <summary>
        /// The confidence value (probability to be a marker) 
        /// </summary>
        public float cf;

        /// <summary>
        /// Th center of marker (in ideal screen coordinates) 
        /// </summary>
        public fixed float pos[2];

        /// <summary>
        /// The line equations for four side of the marker (in ideal screen coordinates) 
        /// probably 3 points to define a line
        /// Ax + By - D = 0 (?)
        /// OR Ax + By = D
        /// </summary>
        public fixed float line[12];
        
        /// <summary>
        /// The edge points of the marker (in ideal screen coordinates) 
        /// </summary>
        public fixed float vertex[8];
    }
}