using System;
using System.Diagnostics;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace Vision.Kinect
{
    public static class Matcher
    {
        public static Bitmap Draw(Bitmap modelImageData, Bitmap observedImageData, out long matchTime)
        {
            var modelImage = new Image<Gray, byte>(modelImageData);
            var observedImage = new Image<Gray, byte>(observedImageData);
            
            return Draw(modelImage, observedImage, out matchTime).Bitmap;
        }

        public static void Foo(Bitmap modelImageData, Bitmap observedImageData)
        {
            var modelImage = new Image<Gray, byte>(modelImageData);
            var observedImage = new Image<Gray, byte>(observedImageData);
            var result = observedImage.MatchTemplate(modelImage, TM_TYPE.CV_TM_CCORR);
            
            CvInvoke.cvNormalize(result.Ptr, result.Ptr, 0, 1, NORM_TYPE.CV_MINMAX, new Image<Gray, byte>(result.Width, result.Height).Ptr);

            var min = double.MinValue;
            var max = double.MaxValue;
            var minLoc = new Point();
            var maxLoc = new Point();

            CvInvoke.cvMinMaxLoc(result.Ptr, ref min, ref max, ref minLoc, ref maxLoc, new Image<Gray, byte>(result.Width, result.Height).Ptr);

            Console.WriteLine("{0}", maxLoc);
        }

        public static Image<Bgr, Byte> Draw(Image<Gray, Byte> modelImage, Image<Gray, byte> observedImage)
        {
            HomographyMatrix homography = null;

            FastDetector fastCPU = new FastDetector(10, true);
            VectorOfKeyPoint modelKeyPoints;
            VectorOfKeyPoint observedKeyPoints;
            Matrix<int> indices;

            BriefDescriptorExtractor descriptor = new BriefDescriptorExtractor();

            Matrix<byte> mask;
            int k = 2;
            double uniquenessThreshold = 0.8;

            //extract features from the object image
            modelKeyPoints = fastCPU.DetectKeyPointsRaw(modelImage, null);
            Matrix<Byte> modelDescriptors = descriptor.ComputeDescriptorsRaw(modelImage, null, modelKeyPoints);

            // extract features from the observed image
            observedKeyPoints = fastCPU.DetectKeyPointsRaw(observedImage, null);
            Matrix<Byte> observedDescriptors = descriptor.ComputeDescriptorsRaw(observedImage, null, observedKeyPoints);
            BruteForceMatcher<Byte> matcher = new BruteForceMatcher<Byte>(DistanceType.L2);
            matcher.Add(modelDescriptors);

            indices = new Matrix<int>(observedDescriptors.Rows, k);
            using (Matrix<float> dist = new Matrix<float>(observedDescriptors.Rows, k))
            {
                matcher.KnnMatch(observedDescriptors, indices, dist, k, null);
                mask = new Matrix<byte>(dist.Rows, 1);
                mask.SetValue(255);
                Features2DToolbox.VoteForUniqueness(dist, uniquenessThreshold, mask);
            }

            int nonZeroCount = CvInvoke.cvCountNonZero(mask);
            if (nonZeroCount >= 4)
            {
                nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints, indices, mask, 1.5, 20);
                if (nonZeroCount >= 4)
                    homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(
                    modelKeyPoints, observedKeyPoints, indices, mask, 2);
            }

            //Draw the matched keypoints
            Image<Bgr, Byte> result = Features2DToolbox.DrawMatches(modelImage, modelKeyPoints, observedImage, observedKeyPoints,
               indices, new Bgr(255, 255, 255), new Bgr(255, 255, 255), mask, Features2DToolbox.KeypointDrawType.DEFAULT);

            #region draw the projected region on the image
            if (homography != null)
            {  //draw a rectangle along the projected model
                Rectangle rect = modelImage.ROI;
                PointF[] pts = new PointF[] {
         new PointF(rect.Left, rect.Bottom),
         new PointF(rect.Right, rect.Bottom),
         new PointF(rect.Right, rect.Top),
         new PointF(rect.Left, rect.Top)};
                homography.ProjectPoints(pts);

                result.DrawPolyline(Array.ConvertAll<PointF, Point>(pts, Point.Round), true, new Bgr(Color.Red), 5);
            }
            #endregion

            return result;
        }

        /// <summary>
        /// Draw the model image and observed image, the matched features and homography projection.
        /// </summary>
        /// <param name="modelImage">The model image</param>
        /// <param name="observedImage">The observed image</param>
        /// <param name="matchTime">The output total time for computing the homography matrix.</param>
        /// <returns>The model image and observed image, the matched features and homography projection.</returns>
        private static Image<Bgr, byte> Draw(Image<Gray, byte> modelImage, Image<Gray, byte> observedImage, out long matchTime)
        {
            HomographyMatrix homography = null;

            var surfCpu = new SURFDetector(500, false);

            const int k = 2;
            const double uniquenessThreshold = 0.8;

            //extract features from the object image
            var modelKeyPoints = surfCpu.DetectKeyPointsRaw(modelImage, null);
            var modelDescriptors = surfCpu.ComputeDescriptorsRaw(modelImage, null, modelKeyPoints);

            var watch = Stopwatch.StartNew();

            // extract features from the observed image
            var observedKeyPoints = surfCpu.DetectKeyPointsRaw(observedImage, null);
            var observedDescriptors = surfCpu.ComputeDescriptorsRaw(observedImage, null, observedKeyPoints);
            var matcher = new BruteForceMatcher<float>(DistanceType.L2);
            matcher.Add(modelDescriptors);

            var indices = new Matrix<int>(observedDescriptors.Rows, k);

            var dist = new Matrix<float>(observedDescriptors.Rows, k);
            matcher.KnnMatch(observedDescriptors, indices, dist, k, null);

            var mask = new Matrix<byte>(dist.Rows, 1);
            mask.SetValue(255);

            Features2DToolbox.VoteForUniqueness(dist, uniquenessThreshold, mask);

            var nonZeroCount = CvInvoke.cvCountNonZero(mask.Ptr);
            if (nonZeroCount >= 4)
            {
                nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints,
                    indices, mask, 1.5, 20);
                if (nonZeroCount >= 4)
                    homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(modelKeyPoints,
                        observedKeyPoints, indices, mask, 2);
            }

            watch.Stop();

            //Draw the matched keypoints
            var result = Features2DToolbox.DrawMatches(modelImage, modelKeyPoints, observedImage,
                observedKeyPoints,
                indices, new Bgr(255, 255, 255), new Bgr(255, 255, 255), mask,
                Features2DToolbox.KeypointDrawType.NOT_DRAW_SINGLE_POINTS);

            #region draw the projected region on the image

            if (homography != null)
            {
                //draw a rectangle along the projected model
                var rect = modelImage.ROI;

                PointF[] pts =
                {
                    new PointF(rect.Left, rect.Bottom),
                    new PointF(rect.Right, rect.Bottom),
                    new PointF(rect.Right, rect.Top),
                    new PointF(rect.Left, rect.Top)
                };

                homography.ProjectPoints(pts);

                result.DrawPolyline(Array.ConvertAll(pts, Point.Round), true, new Bgr(Color.Red), 5);
            }

            #endregion

            matchTime = watch.ElapsedMilliseconds;

            return result;
        }
    }
}