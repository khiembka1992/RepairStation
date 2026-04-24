using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Flann;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using NLog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace AI_AOI.Utils
{
    public class MyCV
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static System.Drawing.Point GetCoordinateAfterRotation(System.Drawing.Point p, System.Drawing.Point center, double angle)
        {

            double angleRadian = angle * Math.PI / 180;
            System.Drawing.Point rotatedPoint = new System.Drawing.Point(
            Convert.ToInt32((p.X - center.X) * Math.Cos(angleRadian) + (p.Y - center.Y) * Math.Sin(angleRadian) + center.X),
            Convert.ToInt32(-(p.X - center.X) * Math.Sin(angleRadian) + (p.Y - center.Y) * Math.Cos(angleRadian) + center.Y)
            );
            return rotatedPoint;
        }
        public static System.Drawing.PointF GetCoordinateAfterRotation(System.Drawing.PointF p, System.Drawing.PointF center, double angle)
        {

            double angleRadian = angle * Math.PI / 180;
            System.Drawing.PointF rotatedPoint = new System.Drawing.PointF(
           (float)((p.X - center.X) * Math.Cos(angleRadian) + (p.Y - center.Y) * Math.Sin(angleRadian) + center.X),
           (float)(-(p.X - center.X) * Math.Sin(angleRadian) + (p.Y - center.Y) * Math.Cos(angleRadian) + center.Y)
            );
            return rotatedPoint;
        }

        public static System.Windows.Point GetCoordinateAfterRotation(System.Windows.Point p, System.Windows.Point center, double angle) {

            double angleRadian = angle * Math.PI / 180;
            System.Windows.Point rotatedPoint = new System.Windows.Point(
                (p.X - center.X) * Math.Cos(angleRadian) + (p.Y - center.Y) * Math.Sin(angleRadian) + center.X,
                -(p.X - center.X) * Math.Sin(angleRadian) + (p.Y - center.Y) * Math.Cos(angleRadian) + center.Y
            );
            return rotatedPoint;
        }


        public static RotatedRect GlobalRotatedRectToLocal(RotatedRect inputRect, RotatedRect originRect)
        {
            var dx = inputRect.Center.X - originRect.Center.X;
            var dy = inputRect.Center.Y - originRect.Center.Y;
            double angleRad = originRect.Angle * Math.PI / 180.0; // Đổi dấu để quay ngược lại
            double localX = dx * Math.Cos(angleRad) + dy * Math.Sin(angleRad);
            double localY = -dx * Math.Sin(angleRad) + dy * Math.Cos(angleRad);
            double localAngle = inputRect.Angle - originRect.Angle;
            return new RotatedRect(new PointF((float)localX, (float)localY), inputRect.Size, (float)localAngle);
        }

        public static RotatedRect LocalRotatedRectToGlobal(RotatedRect inputRect, RotatedRect originRect)
        {
            double angleRad = originRect.Angle * Math.PI / 180.0; // Góc dương ngược chiều kim đồng hồ
            double globalX = inputRect.Center.X * Math.Cos(angleRad) - inputRect.Center.Y * Math.Sin(angleRad) + originRect.Center.X;
            double globalY = inputRect.Center.X * Math.Sin(angleRad) + inputRect.Center.Y * Math.Cos(angleRad) + originRect.Center.Y;

            double globalAngle = inputRect.Angle + originRect.Angle;
            return new RotatedRect(new PointF((float)globalX, (float)globalY), inputRect.Size, (float)globalAngle);
        }


        public static bool IsPointInRotatedRect(RotatedRect rotatedRect, PointF pt)
        {
            // Đưa điểm về gốc tọa độ của rect
            float dx1 = pt.X - rotatedRect.Center.X;
            float dy1 = pt.Y - rotatedRect.Center.Y;

            // Quay ngược lại theo góc của rect
            double angleRad1 = -rotatedRect.Angle * Math.PI / 180.0;
            float localX1 = (float)(dx1 * Math.Cos(angleRad1) + dy1 * Math.Sin(angleRad1));
            float localY1 = (float)(-dx1 * Math.Sin(angleRad1) + dy1 * Math.Cos(angleRad1));

            // Kiểm tra trong phạm vi width/2, height/2
            return Math.Abs(localX1) <= rotatedRect.Size.Width / 2.0f && Math.Abs(localY1) <= rotatedRect.Size.Height / 2.0f;
        }

        public static Image<Bgr, byte> GetImageRotateRect(Image<Bgr, byte> inputImg, RotatedRect rotatedRect)
        {
            Image<Bgr, byte> croppedImage = inputImg.Copy(rotatedRect);
            return croppedImage;
        }

        public static double Round(double a, int number)
        {
            for (int i = 0; i <= number; i++)
            {
                a *= 10;
            }
            int b = Convert.ToInt32(a);
            a = b;
            for (int i = 0; i <= number; i++)
            {
                a /= 10;
            }
            return a;
        }

        public static double NormalizeAngle(double angle)
        {
            angle %= 360;

            if (angle > 180)
                angle -= 360;
            else if (angle < -180)
                angle += 360;

            return angle;
        }

        public static BitmapSource Bitmap2BitmapSource(System.Drawing.Bitmap bitmap, bool Release = true)
        {
            BitmapSource bitmapSource = null;
            try
            {
                var pixelFormat = bitmap.PixelFormat;
                System.Windows.Media.PixelFormat format = PixelFormats.Bgr24;
                switch (pixelFormat)
                {
                    case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                        format = PixelFormats.Gray8;
                        break;
                    case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                        format = PixelFormats.Bgr24;
                        break;
                    case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                        format = PixelFormats.Bgra32;
                        break;
                    default:
                        break;
                }
                var bitmapData = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
                bitmapSource = BitmapSource.Create(
                   bitmapData.Width, bitmapData.Height,
                   bitmap.HorizontalResolution, bitmap.VerticalResolution,
                   format, null,
                   bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);
                bitmap.UnlockBits(bitmapData);
                if (Release)
                {
                    bitmap.Dispose();
                }
            }
            catch (Exception e)
            {
                Logger.Debug("Convert Bitmap to bitmapsource error:" + e.ToString());
            }

            //GC.Collect();
            //GC.WaitForPendingFinalizers();
            return bitmapSource;
        }
        public static BitmapImage LoadImageFromUrl(string imageUrl)
        {
            try
            {
                // Create a new BitmapImage
                BitmapImage bitmapImage = new BitmapImage();

                // Set the BitmapImage URI source
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(imageUrl);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                return bitmapImage;
            }
            catch (Exception ex)
            {
                // Handle exception (e.g., log or display error message)
                Logger.Debug(ex.ToString());
                return null;
            }
        }

        public static double AngleBetween2(System.Windows.Vector from, System.Windows.Vector to)
        {
            return System.Windows.Vector.AngleBetween(from, to);
        }
        public static Image<Bgr, byte> RotateAndShiftImage(Image<Bgr, byte> image, double angle, Point center, int shiftX, int shiftY)
        {
            Mat rotationMatrix = new Mat();
            CvInvoke.GetRotationMatrix2D(new PointF(center.X, center.Y), angle, 1.0, rotationMatrix);
            double[] rotArray = new double[6];
            Marshal.Copy(rotationMatrix.DataPointer, rotArray, 0, 6);
            rotArray[2] += shiftX;
            rotArray[5] += shiftY;
            Mat combinedMatrix = new Mat(2, 3, DepthType.Cv64F, 1);
            Marshal.Copy(rotArray, 0, combinedMatrix.DataPointer, 6);
            Image<Bgr, byte> transformedImage = new Image<Bgr, byte>(image.Size);
            CvInvoke.WarpAffine(image.Mat, transformedImage.Mat, combinedMatrix, image.Size, Inter.Linear, Warp.Default, BorderType.Constant, new MCvScalar(0, 0, 0));

            return transformedImage;
        }

        public static (VectorOfKeyPoint, Mat) GetKeyPointTemplate(Image<Bgr, byte> tmpImg, DetectAlgoritm detectAlgoritm, int maxFeatures = 5000, double ratio = 1)
        {

            VectorOfKeyPoint tmpKps = new VectorOfKeyPoint();
            Mat tmpDescs = new Mat();
            int scaledTmpW = Convert.ToInt32(tmpImg.Width * ratio);
            int scaledTmpH = Convert.ToInt32(tmpImg.Height * ratio);
            //using (VectorOfKeyPoint tmpKps = new VectorOfKeyPoint())
            //using (Mat tmpDescs = new Mat())
            using (Image<Gray, byte> tmpGray = new Image<Gray, byte>(tmpImg.Size))
            using (Image<Gray, byte> tmpGrayScaled = new Image<Gray, byte>(scaledTmpW, scaledTmpH))
            {
                //using (VectorOfKeyPoint tmpKps = new VectorOfKeyPoint())
                //using (Mat tmpDescs = new Mat())
                CvInvoke.CvtColor(tmpImg, tmpGray, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
                CvInvoke.Resize(tmpGray, tmpGrayScaled, tmpGrayScaled.Size, fx: ratio, fy: ratio,
                        interpolation: Emgu.CV.CvEnum.Inter.Linear);
                Feature2D detector = null;
                switch (detectAlgoritm)
                {
                    case DetectAlgoritm.ORB:
                        detector = new ORBDetector(maxFeatures, WTK_A: 4);
                        break;
                    case DetectAlgoritm.KAZE:
                        detector = new KAZE();
                        break;
                    case DetectAlgoritm.AKAZE:
                        detector = new AKAZE();
                        break;
                    default:
                        detector = new ORBDetector(maxFeatures, WTK_A: 4);
                        break;
                }
                detector.DetectAndCompute(tmpGrayScaled, null, tmpKps, tmpDescs, false);
                return (tmpKps, tmpDescs);

            }

        }


        public static AlignmentInfor AlignImage(Image<Bgr, byte> inputImg, VectorOfKeyPoint tmpKps, Mat tmpDescs, DetectAlgoritm detectAlgoritm,
   int maxFeatures = 500000, double ratio = 1, double keep = 0.1)
        {
            try
            {
                int scaledInImgW = Convert.ToInt32(inputImg.Width * ratio);
                int scaledInImgH = Convert.ToInt32(inputImg.Height * ratio);
                using (Image<Gray, byte> inputGray = new Image<Gray, byte>(inputImg.Size))
                using (Image<Gray, byte> inputGrayScaled = new Image<Gray, byte>(scaledInImgW, scaledInImgH))
                using (VectorOfKeyPoint inKps = new VectorOfKeyPoint())
                using (Mat inDescs = new Mat())

                {
                    CvInvoke.CvtColor(inputImg, inputGray, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
                    CvInvoke.Resize(inputGray, inputGrayScaled, inputGrayScaled.Size, fx: ratio, fy: ratio,
                        interpolation: Emgu.CV.CvEnum.Inter.Linear);
                    Feature2D detector = null;
                    if (detectAlgoritm == DetectAlgoritm.ORB)
                    {
                        detector = new ORBDetector(maxFeatures, WTK_A: 4);

                    }
                    else if (detectAlgoritm == DetectAlgoritm.KAZE)
                    {
                        detector = new KAZE();

                    }
                    else
                    {
                        detector = new AKAZE();


                    }

                    detector.DetectAndCompute(inputGrayScaled, null, inKps, inDescs, false);
                    // detector.DetectAndCompute(tmpGrayScaled, null, tmpKps, tmpDescs, false);

                    List<PointF> inPts = new List<PointF>();
                    List<PointF> tmpPts = new List<PointF>();


                    if ((detectAlgoritm == DetectAlgoritm.ORB) | (detectAlgoritm == DetectAlgoritm.AKAZE))
                    {
                        VectorOfDMatch matches = new VectorOfDMatch();
                        BFMatcher matcher = new BFMatcher(DistanceType.Hamming2, true);
                        matcher.Match(inDescs, tmpDescs, matches);
                        MDMatch[] matchesArr = matches.ToArray();
                        Array.Sort(matchesArr, delegate (MDMatch inMatch, MDMatch tmpMatch)
                        {
                            return inMatch.Distance.CompareTo(tmpMatch.Distance);
                        });

                        double keepIdx = matchesArr.Length * keep;


                        for (int i = 0; i < keepIdx; i++)
                        {
                            MDMatch match = matchesArr[i];
                            inPts.Add(inKps[match.QueryIdx].Point);
                            tmpPts.Add(tmpKps[match.TrainIdx].Point);
                        }

                    }
                    else
                    {
                        VectorOfVectorOfDMatch matches = new VectorOfVectorOfDMatch();
                        List<MDMatch> goodList = new List<MDMatch>();
                        var ip = new LinearIndexParams();
                        SearchParams sp = new SearchParams();
                        FlannBasedMatcher matcher = new FlannBasedMatcher(ip, sp);
                        matcher.Add(tmpDescs);
                        matcher.KnnMatch(inDescs, matches, 2);
                        MDMatch[][] matchesArray = matches.ToArrayOfArray();
                        for (int i = 0; i < matchesArray.Length; i++)
                        {
                            MDMatch first = matchesArray[i][0];
                            float dist1 = matchesArray[i][0].Distance;
                            float dist2 = matchesArray[i][1].Distance;

                            if (dist1 < 0.7 * dist2)
                            {
                                goodList.Add(first);
                            }
                        }

                        foreach (MDMatch match in goodList)
                        {
                            inPts.Add(inKps[match.QueryIdx].Point);
                            tmpPts.Add(tmpKps[match.TrainIdx].Point);
                        }


                    }



                    Mat affineMatrix = CvInvoke.EstimateAffinePartial2D(new VectorOfPointF(inPts.ToArray()),
                        new VectorOfPointF(tmpPts.ToArray()), null,
                    Emgu.CV.CvEnum.RobustEstimationAlgorithm.Ransac, 3, 2000, 0.99, 10);

                    Matrix<double> matrix = new Matrix<double>(affineMatrix.Rows, affineMatrix.Cols);

                    affineMatrix.CopyTo(matrix);
                    double scale = Math.Sqrt(matrix[0, 0] * matrix[0, 0] + matrix[0, 1] * matrix[0, 1]);
                    for (int i = 0; i < affineMatrix.Rows; i++)
                    {
                        for (int j = 0; j < affineMatrix.Cols; j++)
                        {
                            matrix[i, j] = matrix[i, j] / scale;
                        }
                    }
                    matrix[0, 2] = matrix[0, 2] / ratio;
                    matrix[1, 2] = matrix[1, 2] / ratio;
                    Image<Bgr, byte> alignedImg = new Image<Bgr, byte>(inputImg.Size);
                    CvInvoke.WarpAffine(inputImg, alignedImg, matrix, inputImg.Size);


                    //get aligment infor
                    Matrix<double> rotationMatrix = new Matrix<double>(2, 2);
                    Matrix<double> invRotationMatrix = new Matrix<double>(2, 2);
                    Matrix<double> invTranslationMatrix = new Matrix<double>(2, 1);
                    for (int i = 0; i < affineMatrix.Rows; i++)
                    {
                        for (int j = 0; j < affineMatrix.Cols - 1; j++)
                        {
                            rotationMatrix[i, j] = matrix[i, j];
                        }
                    }
                    invTranslationMatrix[0, 0] = -matrix[0, 2];
                    invTranslationMatrix[1, 0] = -matrix[1, 2];


                    CvInvoke.Invert(rotationMatrix, invRotationMatrix, Emgu.CV.CvEnum.DecompMethod.Cholesky);

                    Matrix<double> outmtx = rotationMatrix.Mul(invRotationMatrix);

                    AlignmentInfor alignmentInfor = new AlignmentInfor {
                        alignedImg = alignedImg
                    };
                    InvInfor invInfor = new InvInfor {
                        invAngle = Math.Acos(Math.Abs(invRotationMatrix[0, 0])) * 180 / Math.PI,
                        invRotationMatrix = invRotationMatrix,
                        invTranslationMatrix = invTranslationMatrix
                    };
                    invInfor.ShiftX = invInfor.invTranslationMatrix[0, 0];
                    invInfor.ShiftY = invInfor.invTranslationMatrix[1, 0];
                    alignmentInfor.invInfor = invInfor;
                    return alignmentInfor;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                AlignmentInfor alignmentInfor = new AlignmentInfor {
                    alignedImg = inputImg.Clone()
                };
                return alignmentInfor;
            }

        }



        public static void InsertRotatedImage(Mat bigImage, Mat smallImage, double x, double y, double angle) {

            Size size = smallImage.Size;
            double radians = angle * Math.PI / 180.0;
            double sin = Math.Abs(Math.Sin(radians));
            double cos = Math.Abs(Math.Cos(radians));
            double newWidth = size.Width * cos + size.Height * sin;
            double newHeight = size.Width * sin + size.Height * cos;

            PointF center = new PointF(size.Width / 2f, size.Height / 2f);
            using (var rotMat = new Matrix<double>(2, 3))
            using (var rotated = new Mat())
            using (var mask = new Mat())
            {
                CvInvoke.GetRotationMatrix2D(center, angle, 1.0, rotMat);

                rotMat[0, 2] += (newWidth - size.Width) / 2.0;
                rotMat[1, 2] += (newHeight - size.Height) / 2.0;

                //Mat rotated = new Mat();
                CvInvoke.WarpAffine(smallImage, rotated, rotMat, new Size((int)newWidth, (int)newHeight));
                mask.Create(rotated.Size.Height, rotated.Size.Width, DepthType.Cv8U, 1);
                mask.SetTo(new MCvScalar(0, 0, 0));

                RotatedRect rotatedRect = new RotatedRect(new PointF((float)(newWidth / 2.0), (float)(newHeight / 2.0)), new SizeF(size.Width, size.Height), (float)-angle);
                PointF[] vertices = rotatedRect.GetVertices();
                Point[] points = vertices.Select(v => new Point((int)v.X, (int)v.Y)).ToArray();
                CvInvoke.FillPoly(mask, new VectorOfPoint(points), new MCvScalar(255, 255, 255));

                int offsetX = (int)(x - rotated.Width / 2.0);
                int offsetY = (int)(y - rotated.Height / 2.0);

                // Kiểm tra nếu ảnh nhỏ bị vượt ngoài biên ảnh lớn
                if (offsetX < 0 || offsetY < 0 || offsetX + rotated.Width > bigImage.Width || offsetY + rotated.Height > bigImage.Height)
                    throw new ArgumentException("The inserted image exceeds the boundaries of the big image!");

                // Tạo vùng ROI trên ảnh lớn
                using (Mat roi = new Mat(bigImage, new Rectangle(offsetX, offsetY, rotated.Width, rotated.Height)))
                {
                    rotated.CopyTo(roi, mask);
                }
            }
        }

    }





    public static class MathHelper
    {
        public static float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }
    }

    enum MatcherAlgorithm
    {
        BF_MATCHER,
        FLANN_MATCHER
    }
    public enum DetectAlgoritm
    {
        ORB,
        KAZE,
        AKAZE
    }

    public class InvInfor
    {
        public Matrix<double> invRotationMatrix;
        public Matrix<double> invTranslationMatrix;
        public double invAngle;
        public double ShiftX;
        public double ShiftY;
    }

    public class AlignmentInfor
    {
        public InvInfor invInfor;
        public Image<Bgr, byte> alignedImg;
    }
}
