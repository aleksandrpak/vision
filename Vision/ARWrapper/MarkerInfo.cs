using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace ARWrapper
{
    public struct MarkerInfo
    {
        public int Id { get; private set; }

        public MarkerPoint Center { get; private set; }

        public MarkerPoint TopLeftEdge { get; private set; }

        public MarkerPoint TopRightEdge { get; private set; }

        public MarkerPoint BottomLeftEdge { get; private set; }

        public MarkerPoint BottomRightEdge { get; private set; }

        public float Confidence { get; private set; }

        public static implicit operator MarkerInfo(NativeMarkerInfo nativeInfo)
        {
            var markerInfo = new MarkerInfo();

            unsafe
            {
                markerInfo.Id = nativeInfo.id;
                markerInfo.Confidence = nativeInfo.cf;
                markerInfo.Center = new MarkerPoint(nativeInfo.pos[0], nativeInfo.pos[1]);

                var points = new MarkerPoint[4];
                for (var i = 0; i < 4; ++i)
                    points[i] = new MarkerPoint(nativeInfo.vertex[i*2], nativeInfo.vertex[i*2 + 1]);

                var topPoints = points.OrderBy(i => i.Y).ThenBy(i => i.X);
                var bottomPoints = points.Except(topPoints).OrderBy(i => i.X);

                markerInfo.TopLeftEdge = topPoints.First();
                markerInfo.TopRightEdge = topPoints.Last();

                markerInfo.BottomLeftEdge = bottomPoints.First();
                markerInfo.BottomRightEdge = bottomPoints.Last();
            }

            return markerInfo;
        }
    }
}