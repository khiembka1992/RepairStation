using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Emgu.CV.Structure;
using AI_AOI.Utils;

namespace Andrew.Controls
{

    public partial class ImageBox : UserControl
    {


        public double mScale = 1;
        private double mMaxZoomScale = 10;
        private double mMinZoomScale = 0.01;
        private bool mIsDrawing = false;
        private Point mStartPoint = new Point(0, 0);
        private Point mEndPoint = new Point(0, 0);
        private double mAngleStart = 0;
        private RotateTransform _rotateTransform = new RotateTransform {Angle = 0, CenterX = 0, CenterY = 0};

        public event ClickHandler Clicked;
        public event SelectRectangleHandler SelectRectangleChanged;
        public event EventHandler BulkAllOkRequested;
        public event EventHandler BulkAllNgRequested;
        private ObservableCollection<MyDrawedRectangle> _DrawedRectangles;
        private ObservableCollection<MyDrawedText> _DrawedTexts;
        private ObservableCollection<MyDrawedImage> _DrawedImages;
        private ObservableCollection<MyDrawedPolylines> _DrawedPolylines;
        private double _DefaultScale = 1;
        private Bitmap _Bitmap;
        private bool _IsFirstTime = true;




        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
            "ImageSource", typeof(BitmapSource), typeof(ImageBox), new PropertyMetadata(ImageSourceChanged));

        public static readonly DependencyProperty ShowBulkConfirmMenuProperty = DependencyProperty.Register(
            nameof(ShowBulkConfirmMenu),
            typeof(bool),
            typeof(ImageBox),
            new PropertyMetadata(false, OnShowBulkConfirmMenuChanged));

        public bool ShowBulkConfirmMenu
        {
            get => (bool)GetValue(ShowBulkConfirmMenuProperty);
            set => SetValue(ShowBulkConfirmMenuProperty, value);
        }

        private static void OnShowBulkConfirmMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageBox box)
            {
                box.UpdateBulkMenuVisibility((bool)e.NewValue);
            }
        }

        private static void ImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageBox imbox = (ImageBox)d;
            if (imbox != null)
            {
                var newValue = (BitmapSource)e.NewValue;


                imbox.OnImageSourceChanged(newValue);
            }
        }

        public BitmapSource ImageSource
        {
            get
            {
                return (BitmapSource)GetValue(ImageSourceProperty);
            }
            set
            {

                SetValue(ImageSourceProperty, value);
                if (value != null & _IsFirstTime)
                {
                    FillScreen();
                    _IsFirstTime = false;
                }
                   
               
            }
        }

     
        private void OnImageSourceChanged(BitmapSource bmSource)
        {


            _Bitmap?.Dispose();
            canvas.Children.Clear();

            _Bitmap = BitmapSourceToBitmap(bmSource);
            if(bmSource!= null)
                _DefaultScale = bmSource.Width / bmSource.PixelWidth;
     
            //if(bmSource!= null)
            //    ScaleFit(bmSource.Width, bmSource.Height);
        }






        public static readonly DependencyProperty DrawedRectanglesProperty = DependencyProperty.Register(
           "DrawedRectangles", typeof(ObservableCollection<MyDrawedRectangle>), typeof(ImageBox), new PropertyMetadata(new ObservableCollection<MyDrawedRectangle>(), DrawedRectanglesChanged));



        public ObservableCollection<MyDrawedRectangle> DrawedRectangles
        {
            get { return (ObservableCollection<MyDrawedRectangle>)GetValue(DrawedRectanglesProperty); }
            set
            {
                SetValue(DrawedRectanglesProperty, value);
            }


        }



        public static readonly DependencyProperty DrawedImagesProperty = DependencyProperty.Register(
           "DrawedImages", typeof(ObservableCollection<MyDrawedImage>), typeof(ImageBox), new PropertyMetadata(new ObservableCollection<MyDrawedImage>(),
               DrawedImagesChanged));


        public ObservableCollection<MyDrawedImage> DrawedImages
        {
            get { return (ObservableCollection<MyDrawedImage>)GetValue(DrawedImagesProperty); }
            set
            {
                SetValue(DrawedImagesProperty, value);
                
            }


        }


        public static readonly DependencyProperty DrawedPolyLinesProperty = DependencyProperty.Register(
           "DrawedPolyLines", typeof(ObservableCollection<MyDrawedPolylines>), typeof(ImageBox), new PropertyMetadata(new ObservableCollection<MyDrawedPolylines>(),
               DrawedPolyLinesChanged));


        public ObservableCollection<MyDrawedPolylines> DrawedPolyLines
        {
            get { return (ObservableCollection<MyDrawedPolylines>)GetValue(DrawedPolyLinesProperty); }
            set
            {
                SetValue(DrawedPolyLinesProperty, value);
            }


        }

        private static void DrawedPolyLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageBox imbox = (ImageBox)d;
            if (imbox != null)
            {
                var newValue = (ObservableCollection<MyDrawedPolylines>)e.NewValue;
                imbox.OnDrawedPolyLinesChanged(newValue);
            }
        }


        private void OnDrawedPolyLinesChanged(ObservableCollection<MyDrawedPolylines> drawedPolylines)
        {

            if (drawedPolylines == null)
                return;
            foreach (var polyline in _DrawedPolylines)
            {
                canvas.Children.Remove(polyline.DrawedPolyline);
            }

            _DrawedPolylines = drawedPolylines;
            foreach (var polyline in drawedPolylines)
            {

                canvas.Children.Add(polyline.DrawedPolyline);

            }
            RenderPollyLines();
        }

        public static readonly DependencyProperty AngleRectangleProperty = DependencyProperty.Register(
       nameof(AngleRectangle), typeof(double), typeof(ImageBox), new PropertyMetadata(0.0));

        public double AngleRectangle
        {
            get { 
                return (double)GetValue(AngleRectangleProperty); 
            }
            set
            {
                SetValue(AngleRectangleProperty, value);
            }
        }

        public static readonly DependencyProperty GoPointViewProperty = DependencyProperty.Register(
            nameof(GoPointView),
            typeof(Point),
            typeof(ImageBox),
            new PropertyMetadata(new Point(0.0, 0.0), OnGoPointViewChanged));


        public Point GoPointView
        {
            get
            {
                return (Point)GetValue(GoPointViewProperty);
            }
            set
            {
                SetValue(GoPointViewProperty, value);
            }
        }

        private static void OnGoPointViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var imageBox = d as ImageBox;
            if (imageBox != null)
            {
                var newPoint = (Point)e.NewValue;
                imageBox.GoPoint(newPoint, false); // Gọi GoPoint với giá trị mới
            }
        }


        public static readonly DependencyProperty IsSelectedRectangleProperty = DependencyProperty.Register(
          "IsSelectedRectangle", typeof(bool), typeof(ImageBox), new PropertyMetadata(false, OnSelectedRectangleChanged));



        public bool IsSelectedRectangle
        {
            get { return (bool)GetValue(IsSelectedRectangleProperty); }
            set
            {
                SetValue(IsSelectedRectangleProperty, value);
            }


        }
        private static void OnSelectedRectangleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

            ImageBox imbox = (ImageBox)d;
            if (imbox != null)
            {
                var newValue = (bool)e.NewValue;
                imbox.OnSelectedChangeRectangleChanged(newValue);
            }


        }

        private void OnSelectedChangeRectangleChanged(bool newValue)
        {
            if (newValue)
            {
                grid.Cursor = Cursors.Cross;
            }
            else
            {
                grid.Cursor = Cursors.Arrow;
            }
        }

        private static void DrawedRectanglesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageBox imbox = (ImageBox)d;
            if (imbox != null)
            {
                var newValue = (ObservableCollection<MyDrawedRectangle>)e.NewValue;
                imbox.OnDrawedRectanglesChanged(newValue);
            }
        }


        private void OnDrawedRectanglesChanged(ObservableCollection<MyDrawedRectangle> drawedRectangles)
        {

            if (drawedRectangles == null)
                return;
            foreach (var rect in _DrawedRectangles)
            {
                if (rect is null) continue;
                canvas.Children.Remove(rect.DrawedRectangle);
                canvas.Children.Remove(rect.SubDrawedRectangle);
            }

            _DrawedRectangles = drawedRectangles;
            foreach (var rect in drawedRectangles)
            {
                if (rect is null) continue;
                canvas.Children.Add(rect.DrawedRectangle);
                canvas.Children.Add(rect.SubDrawedRectangle);

            }
            RenderDrawedRectangle();
        }


        private static void DrawedImagesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageBox imbox = (ImageBox)d;
            if (imbox != null)
            {
                var newValue = (ObservableCollection<MyDrawedImage>)e.NewValue;
                imbox.OnDrawedImagesChanged(newValue);
            }

        }

        private void OnDrawedImagesChanged(ObservableCollection<MyDrawedImage> drawedImages)
        {
            if (drawedImages == null)
                return;
            foreach (var img in _DrawedImages)
            {
                canvas.Children.Remove(img.DrawedImage);
            }

            List<MyDrawedImage> topDrawedImages = new List<MyDrawedImage>();
            List<MyDrawedImage> topmostImages = new List<MyDrawedImage>();


            foreach (var checkImage in drawedImages)
            {
                if (checkImage.IsTopmost)
                {
                    topmostImages.Add(checkImage);
                }
            }

            foreach (var checkImage in drawedImages)
            {
                foreach(var candidateImage in drawedImages)
                {
                    if (!topmostImages.Contains(candidateImage)&&!topDrawedImages.Contains(candidateImage) && candidateImage.IsFront(checkImage))
                    {
                        topDrawedImages.Add(candidateImage);
                    }
                }
            }

            _DrawedImages = drawedImages;
            foreach (var img in drawedImages)
            {
                if(!topDrawedImages.Contains(img)&&!topmostImages.Contains(img))
                    canvas.Children.Add(img.DrawedImage);

            }
            foreach (var img in topDrawedImages)
            {
                 canvas.Children.Add(img.DrawedImage);

            }
            foreach (var img in topmostImages)
            {
                canvas.Children.Add(img.DrawedImage);

            }
            RenderDrawedImage();
        }





        public static readonly DependencyProperty DrawedTextsProperty = DependencyProperty.Register(
           "DrawedTexts", typeof(ObservableCollection<MyDrawedText>), typeof(ImageBox), new PropertyMetadata(new ObservableCollection<MyDrawedText>(), DrawedTextsChanged));



        public ObservableCollection<MyDrawedText> DrawedTexts
        {
            get { return (ObservableCollection<MyDrawedText>)GetValue(DrawedTextsProperty); }
            set
            {
                SetValue(DrawedTextsProperty, value);
            }


        }

        private static void DrawedTextsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageBox imbox = (ImageBox)d;
            if (imbox != null)
            {
                var newValue = (ObservableCollection<MyDrawedText>)e.NewValue;
                imbox.OnDrawedTextsChanged(newValue);
            }
        }






        private void OnDrawedTextsChanged(ObservableCollection<MyDrawedText> drawedTexts)
        {
            if (drawedTexts == null)
                return;
            foreach (var text in _DrawedTexts)
            {
                canvas.Children.Remove(text.TextBlock);
            }
            _DrawedTexts = drawedTexts;
            foreach (var text in drawedTexts)
            {
                canvas.Children.Add(text.TextBlock);

            }
            RenderDrawedText();
        }





        private void OnSelectedRectangleChanged(RotatedRect newValue)
        {
            if (DataContext is IImageboxViewModel viewModel) {
                viewModel.UpdateSelectedRect(newValue);

            }

        }

        public RotatedRect SelectedRectangle
        {
            set;
            get;
        }


        //end define property for binding






        private void RenderDrawedRectangle()
        {
            foreach (var item in _DrawedRectangles)
            {
                double x = item.OriginX * mScale*_DefaultScale;
                double y = item.OriginY * mScale*_DefaultScale;
                double w = item.OriginWidth * mScale*_DefaultScale;
                double h = item.OriginHeight * mScale*_DefaultScale;
                double thickness = item.OriginThickness * mScale*_DefaultScale;
                item.DrawedRectangle.Width = w;
                item.DrawedRectangle.Height = h;
                item.DrawedRectangle.StrokeThickness = thickness;

                double left = x - w / 2;
                double top = y - h / 2;

                RotateTransform rotateTransform = new RotateTransform
                {
                    Angle = -item.OriginAngle, // Góc xoay
                    CenterX = w / 2, // Tâm xoay theo chiều ngang
                    CenterY = h / 2 // Tâm xoay theo chiều dọc
                };

                // Áp dụng phép xoay cho hình chữ nhật
                item.DrawedRectangle.RenderTransform = rotateTransform;
                Canvas.SetTop(item.DrawedRectangle, top);
                Canvas.SetLeft(item.DrawedRectangle, left);

                item.SubDrawedRectangle.Width = 2*item.DrawedRectangle.StrokeThickness;
                item.SubDrawedRectangle.Height = h;
                item.SubDrawedRectangle.StrokeThickness = thickness;
                //item.SubDrawedRectangle.Fill = item.SubDrawedRectangle.Stroke;
                item.SubDrawedRectangle.RenderTransform = rotateTransform;

                Canvas.SetTop(item.SubDrawedRectangle, top);
                Canvas.SetLeft(item.SubDrawedRectangle, left);
            }
        }

        public void RenderDrawedText()
        {
            foreach (var item in _DrawedTexts)
            {
                double x = item.OriginX * mScale*_DefaultScale;
                double y = item.OriginY * mScale*_DefaultScale;
                double fontSize = item.OriginFontSize * mScale*_DefaultScale;
                item.TextBlock.FontSize = fontSize;
                Canvas.SetTop(item.TextBlock, y);
                Canvas.SetLeft(item.TextBlock, x);
            }
        }

        public void RenderDrawedImage()
        {


            foreach (var item in _DrawedImages)
            {
                double x = item.OriginX * mScale*_DefaultScale;
                double y = item.OriginY * mScale*_DefaultScale;
                double width = item.OriginWidth * mScale*_DefaultScale;
                double height = item.OriginHeight * mScale*_DefaultScale; 
                item.DrawedImage.Width = width;
                item.DrawedImage.Height = height;

                double left = x - width / 2;
                double top = y - height / 2;

                RotateTransform rotateTransform = new RotateTransform
                {
                    Angle = -item.OriginAngle, // Góc xoay
                    CenterX = width / 2, // Tâm xoay theo chiều ngang
                    CenterY = height / 2 // Tâm xoay theo chiều dọc
                };
                item.DrawedImage.RenderTransform = rotateTransform;
                Canvas.SetTop(item.DrawedImage, top);
                Canvas.SetLeft(item.DrawedImage, left);
            }
        }

        public void RenderPollyLines()
        {
            foreach(var polyLine in _DrawedPolylines)
            {
                PointCollection newPointCollection = new PointCollection();
                foreach(var p in polyLine.OriginPointCollection)
                {
                    var newPoint = new Point(p.X * mScale * _DefaultScale, 
                        p.Y * mScale * _DefaultScale);
                    newPointCollection.Add(newPoint);
                }
                polyLine.DrawedPolyline.Points = newPointCollection;
                polyLine.DrawedPolyline.StrokeThickness = polyLine.OriginThickness * mScale * _DefaultScale;
            }
        }


        public void RenderDrawedItems()
        {
            RenderDrawedRectangle();
            RenderDrawedText();
            RenderDrawedImage();
            RenderPollyLines();
        }

        public ImageBox()
        {
            InitializeComponent();


            _DrawedRectangles = new ObservableCollection<MyDrawedRectangle>();
            _DrawedTexts = new ObservableCollection<MyDrawedText>();
            _DrawedImages = new ObservableCollection<MyDrawedImage>();
            _DrawedPolylines = new ObservableCollection<MyDrawedPolylines>();
            SelectByCtr = false;

            rect.RenderTransform = _rotateTransform;
            subrect.RenderTransform = _rotateTransform;
            UpdateBulkMenuVisibility(ShowBulkConfirmMenu);

        }

        private void UpdateBulkMenuVisibility(bool visible)
        {
            if (mnBulkSeparator == null || mnAllOk == null || mnAllNg == null) return;
            var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            mnBulkSeparator.Visibility = visibility;
            mnAllOk.Visibility = visibility;
            mnAllNg.Visibility = visibility;
        }



        public void FillScreen()
        {
            if (imb.Source != null)
            {
                double scaleX = (scroll.ActualWidth - 5) / imb.Source.Width;
                double scaleY = (scroll.ActualHeight - 5) / imb.Source.Height;
                SetScale(Math.Min(scaleX, scaleY));
            }
        }
        public bool SelectByCtr { get; set; }

        public double ZoomScale
        {
            get => mScale;
            set
            {
                if (value <= mMaxZoomScale && value >= mMinZoomScale)
                {
                    SetScale(value);
                }
                else
                {
                    throw new InvalidOperationException("Zoom scale out of range!");
                }
            }
        }
        public double MaxZoomScale
        {
            get => mMaxZoomScale;
            set
            {
                mMaxZoomScale = value;
            }
        }
        public double MinZoomScale
        {
            get => mMinZoomScale;
            set
            {
                mMinZoomScale = value;
            }
        }




        public System.Drawing.Bitmap SourceFromBitmap
        {
            set
            {
                imb.Source = Bitmap2BitmapSource(value);
            }
        }



        private Point GetMousePositionRelativeToContent()
        {
            // Get the position of the mouse relative to the Grid containing the ScrollViewer
            Point mousePositionRelativeToGrid = Mouse.GetPosition(grid);

            // Get the size of the content within the ScrollViewer
            double contentWidth = scroll.ExtentWidth;
            double contentHeight = scroll.ExtentHeight;

            // Calculate the position of the mouse relative to the content within the ScrollViewer
            double scaleX = contentWidth / scroll.ActualWidth;
            double scaleY = contentHeight / scroll.ActualHeight;

            double offsetX = scroll.HorizontalOffset;
            double offsetY = scroll.VerticalOffset;

            double mouseX = (mousePositionRelativeToGrid.X + offsetX) * scaleX;
            double mouseY = (mousePositionRelativeToGrid.Y + offsetY) * scaleY;

            return new Point(mouseX, mouseY);
        }


        private void SetScale(double Scale)
        {
            if (imb.Source != null)
            {
                if (Scale <= mMaxZoomScale && Scale >= mMinZoomScale)
                {
                    //Scale = Math.Round(Scale, 2);
                    grid.Width = imb.Source.Width * Scale;
                    grid.Height = imb.Source.Height * Scale;
                    Point pointbefor = GetMousePoint();
                    mScale = Scale;
                    SetRectSelected();
                    RenderDrawedItems();
                    scroll.UpdateLayout();
                    grid.UpdateLayout();
                    Point mousePositionRelativeToScrollViewer = Mouse.GetPosition(scroll);
                    Point point = GetMousePoint();
                    var newOffsetX = pointbefor.X * mScale - mousePositionRelativeToScrollViewer.X;
                    var newOffsetY = pointbefor.Y * mScale - mousePositionRelativeToScrollViewer.Y;
                    scroll.ScrollToVerticalOffset(newOffsetY);
                    scroll.ScrollToHorizontalOffset(newOffsetX);

                }
            }
        }


        private void GetRectSelected()
        {
            double centerX = (mStartPoint.X + mEndPoint.X) / 2;
            double centerY = (mStartPoint.Y + mEndPoint.Y) / 2;
            SelectedRectangle = new RotatedRect(new PointF((float)(centerX / mScale/_DefaultScale), (float)(centerY / mScale/_DefaultScale)), new SizeF((float)(rect.Width / mScale/_DefaultScale), (float)(rect.Height / mScale/_DefaultScale)), -(float)AngleRectangle);
            //Console.WriteLine(SelectedRectangle);
        }
        public void SetRectSelected()
        {
            rect.Visibility = Visibility.Hidden;
            subrect.Visibility = Visibility.Hidden;

        }
        public Point GetMousePoint()
        {
            try {
                var P = Mouse.GetPosition(imb);
                double x = P.X / mScale / _DefaultScale;
                double y = P.Y / mScale / _DefaultScale;
                return new Point(x, y);
            }
            catch {
                return new Point(0, 0);
            }
        }
        public static BitmapSource Bitmap2BitmapSource(System.Drawing.Bitmap bitmap, bool Release = true)
        {
            var pixelFormat = bitmap.PixelFormat;
            PixelFormat format = PixelFormats.Bgr24;
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
            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                format, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);
            bitmap.UnlockBits(bitmapData);
            if (Release)
            {
                bitmap.Dispose();
            }
            //GC.Collect();
            //GC.WaitForPendingFinalizers();
            return bitmapSource;
        }

        public static Bitmap BitmapSourceToBitmap(BitmapSource bitmapSource)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                // Save the BitmapSource to the MemoryStream
                if (bitmapSource == null)
                    return null;
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(memoryStream);

                // Create a Bitmap from the MemoryStream
                memoryStream.Seek(0, SeekOrigin.Begin);
                Bitmap bitmap = new Bitmap(memoryStream);

                return bitmap;
            }
        }

        public Brush RectangleStroke
        {
            get => rect.Stroke;
            set
            {
                rect.Stroke = value;
            }
        }
        private void mnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            SetScale(1.2 * mScale);
        }

        private void mnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            SetScale(0.8 * mScale);
        }

        private void mnReset_Click(object sender, RoutedEventArgs e)
        {
            SetScale(1);
        }
        private void mnResetSelection_Click(object sender, RoutedEventArgs e)
        {
            SelectedRectangle = new RotatedRect();
        }
        private void imb_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (IsSelectedRectangle)
            {
                if (e.ChangedButton == MouseButton.Left) {
                    mStartPoint = Mouse.GetPosition(grid);
                    mEndPoint = Mouse.GetPosition(grid);
                    mIsDrawing = true;
                }
            }
        }
        private void imb_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsSelectedRectangle) {
                if (e.ChangedButton == MouseButton.Left) {
                    mIsDrawing = false;

                    GetRectSelected();
                    SetRectSelected();
                    var args = new SelectRectangleArgs(SelectedRectangle);
                    RectangleChanged(this, args);
                    OnSelectedRectangleChanged(SelectedRectangle);
                }
                if (e.ChangedButton == MouseButton.Right) {
                    e.Handled = true;
                }
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                MouseClicked(this, new ClickEventArgs(GetMousePoint()));
            }
        }


        private void imb_PreviewMouseMove(object sender, MouseEventArgs e)
        {   
            if (mIsDrawing && IsSelectedRectangle)
            {
                if (e.RightButton == MouseButtonState.Released) {
                    mEndPoint = Mouse.GetPosition(grid);
                    var CenterPoint = new Point((mStartPoint.X + mEndPoint.X) / 2, (mStartPoint.Y + mEndPoint.Y) / 2);

                    double x = CenterPoint.X;
                    double y = CenterPoint.Y;

                    var np = MyCV.GetCoordinateAfterRotation(new PointF((float)mEndPoint.X, (float)mEndPoint.Y), new PointF((float)mStartPoint.X, (float)mStartPoint.Y), -AngleRectangle);
                    double w = Math.Abs(np.X - mStartPoint.X);
                    double h = Math.Abs(np.Y - mStartPoint.Y);

                    double left = x - w / 2;
                    double top = y - h / 2;

                    rect.Width = w;
                    rect.Height = h;
                    rect.Margin = new Thickness(left, top, 0, 0);


                    // Hiển thị hình chữ nhật nếu nó bị ẩn
                    rect.Visibility = Visibility.Visible;

                    subrect.Width = 2*rect.StrokeThickness;
                    subrect.Height = h;
                    subrect.Margin = new Thickness(left, top, 0, 0);

                    subrect.Visibility = Visibility.Visible;
                    _rotateTransform.CenterX = w / 2;
                    _rotateTransform.CenterY = h / 2;
                    _rotateTransform.Angle = -AngleRectangle;
                    mAngleStart = AngleRectangle;
                }
                else {
                    var CenterPoint = new Point((mStartPoint.X + mEndPoint.X) / 2, (mStartPoint.Y + mEndPoint.Y) / 2);
                    var CurrentPoint = Mouse.GetPosition(grid);

                    double BAx = CurrentPoint.X - CenterPoint.X;
                    double BAy = CurrentPoint.Y - CenterPoint.Y;
                    double BCx = mEndPoint.X - CenterPoint.X;
                    double BCy = mEndPoint.Y - CenterPoint.Y;

                    double dot = (BAx * BCx) + (BAy * BCy);
                    double cross = (BAx * BCy) - (BAy * BCx);
                    double angleRadians = Math.Atan2(cross, dot);
                    var angleDeg = angleRadians * (180 / Math.PI);
                    AngleRectangle = mAngleStart + angleDeg;
                    _rotateTransform.Angle = -AngleRectangle;
                }
            }

            Point mp = GetMousePoint();


            if (_Bitmap == null)
                return;
            try
            {
                if (mp.X > 0 && mp.X < _Bitmap.Width / _DefaultScale && mp.Y > 0 && mp.Y < _Bitmap.Height / _DefaultScale)
                {
                    System.Drawing.Color pixelColor = _Bitmap.GetPixel((int)mp.X, (int)mp.Y);
                    if (DataContext is IImageboxViewModel viewModel)
                    {
                        viewModel.UpdateMouseInfor((int)mp.X, (int)mp.Y, pixelColor.A, pixelColor.R, pixelColor.G, pixelColor.B);

                    }
                }
                else {
                    if (DataContext is IImageboxViewModel viewModel) {
                        viewModel.UpdateMouseInfor((int)mp.X, (int)mp.Y, 0, 0, 0, 0);

                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

            }




        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                if (SelectByCtr && imb.Source != null)
                {
                    //IsSelectedRectangle = true;
                    var vm = DataContext as IImageboxViewModel;
                    vm?.UpdateIsSelectedRectState(true);
                }
            }
        }

        private void UserControl_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                if (SelectByCtr)
                {
                    //IsSelectedRectangle = false;
                    var vm = DataContext as IImageboxViewModel;
                    vm?.UpdateIsSelectedRectState(false);
                }
            }
        }
        public void GoPoint(Point P, bool ResetScale = false)
        {
            if (ResetScale)
            {
                SetScale(1);
            }
            double x = P.X * mScale;
            double y = P.Y * mScale;
            double crOffsetX = scroll.HorizontalOffset;
            double crOffsetY = scroll.VerticalOffset;
            double lX = scroll.ViewportWidth;
            double lY = scroll.ViewportHeight;
            double offsetX = crOffsetX + lX / 2;
            double offsetY = crOffsetY + lY / 2;
            if (x > lX / 2)
            {
                scroll.ScrollToHorizontalOffset(x - lX / 2);
            }
            if (y > lY / 2)
            {
                scroll.ScrollToVerticalOffset(y - lY / 2);
            }
        }

        private void scroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            //Console.WriteLine($"H:{scroll.HorizontalOffset}, V:{scroll.VerticalOffset}, W:{scroll.ViewportWidth}");
        }
        protected void MouseClicked(object sender, ClickEventArgs e)
        {
            this.Clicked?.Invoke(sender, e);
        }
        protected void RectangleChanged(object sender, SelectRectangleArgs e)
        {
            this.SelectRectangleChanged?.Invoke(sender, e);
        }

        private void mnFillScreen_Click(object sender, RoutedEventArgs e)
        {
            FillScreen();
        }

        private void mnAllOk_Click(object sender, RoutedEventArgs e)
        {
            BulkAllOkRequested?.Invoke(this, EventArgs.Empty);
        }

        private void mnAllNg_Click(object sender, RoutedEventArgs e)
        {
            BulkAllNgRequested?.Invoke(this, EventArgs.Empty);
        }

        private void UserControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                double zoomSpeed = e.Delta / 120;
                double newScale = zoomSpeed>0 ? mScale * 1.2*zoomSpeed: -mScale * 0.8 * zoomSpeed;
                Point originPoint = GetMousePoint();

                SetScale(newScale);
            }
        }
  
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {

            FillScreen();
        }

        private void rotateThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Console.WriteLine(e.HorizontalChange);
        }
    }

    public class MyDrawedRectangle
    {

        public MyDrawedRectangle(double x, double y, double w, double h, double angle)
        {
            OriginWidth = w;
            OriginHeight = h;
            OriginX = x;
            OriginY = y;
            OriginAngle = angle;
            DrawedRectangle = new System.Windows.Shapes.Rectangle();
            SubDrawedRectangle = new System.Windows.Shapes.Rectangle();
        }

        public MyDrawedRectangle(double x, double y, double w, double h, double angle, Brush color, int thickness)
        {
            OriginWidth = w;
            OriginHeight = h;
            OriginX = x;
            OriginY = y;
            OriginAngle = angle;
            OriginThickness = thickness;
            DrawedRectangle = new System.Windows.Shapes.Rectangle {
                Stroke = color,
                StrokeThickness = thickness
            };
            SubDrawedRectangle = new System.Windows.Shapes.Rectangle {
                Stroke = color,
                StrokeThickness = thickness
            };

        }

        public MyDrawedRectangle(double x, double y, double w, double h, double angle, Brush color, Brush fillColor, int thickness) : this(x, y, w, h, angle, color, thickness)
        {

            DrawedRectangle.Fill = fillColor;
            SubDrawedRectangle.Fill = fillColor;
        }
        public System.Windows.Shapes.Rectangle DrawedRectangle { get; set; }
        public System.Windows.Shapes.Rectangle SubDrawedRectangle { get; set; }
        public double OriginWidth { get; set; }
        public double OriginHeight { get; set; }
        public double OriginX { get; set; }
        public double OriginY { get; set; }
        public double OriginAngle { get; set; }
        public double OriginThickness { get; set; }


    }

    public class MyDrawedText
    {

        public MyDrawedText(string text, double x, double y, Brush color, double fontSize)
        {
            TextBlock = new TextBlock {
                Text = text,
                Foreground = color,
                Background = Brushes.Transparent,
                //BorderBrush = Brushes.Transparent,
                //BorderThickness = new Thickness(0)
            };
            OriginFontSize = fontSize;
            OriginX = x;
            OriginY = y;
        }
        public TextBlock TextBlock { get; set; }
        public double OriginFontSize { get; set; }
        public double OriginX { get; set; }
        public double OriginY { get; set; }
    }


    public class MyDrawedImage
    {
        public MyDrawedImage(System.Windows.Controls.Image image, double x, double y, double w, double h, double angle, bool isTopmost)
        {
            DrawedImage = image;
            OriginX = x;
            OriginY = y;
            OriginWidth = w;
            OriginHeight = h;
            OriginAngle = angle;
            IsTopmost = isTopmost; 
        }


        public bool IsFront(MyDrawedImage checkImage)
        {
            var candidateRect = new System.Drawing.Rectangle((int)OriginX,(int)OriginY,(int)OriginWidth,(int)OriginHeight);
            var checkRect = new System.Drawing.Rectangle((int)checkImage.OriginX, (int)checkImage.OriginY, 
                (int)checkImage.OriginWidth, (int)checkImage.OriginHeight);

            if (candidateRect!= checkRect && (checkRect.Contains(candidateRect)|| 
                (checkRect.IntersectsWith(candidateRect)&&(checkRect.Width*checkRect.Height > candidateRect.Width*candidateRect.Height))))
            {
                return true;
            }
            else
            {
                return false;
            }
            


        }

        public System.Windows.Controls.Image DrawedImage { get; set; }

        public double OriginX { get; set; }

        public double OriginY { get; set; }

        public double OriginWidth { get; set; }
        public double OriginHeight { get; set; }
        public double OriginAngle { get; set; }
        public bool IsTopmost { get; set; }
    }

    public class MyDrawedPolylines
    {
        public PointCollection OriginPointCollection { get; set; }
        public Polyline DrawedPolyline { get; set; }
        public double OriginThickness { get; set; }
        public MyDrawedPolylines(PointCollection originPointCollection, Brush color, int thickness)
        {
            OriginPointCollection = originPointCollection;
            OriginThickness = thickness;
            DrawedPolyline = new Polyline
            {
                Points = originPointCollection,
                Stroke = color,
               
            };
        }

    }



    public delegate void ClickHandler(object sender, ClickEventArgs e);
    public class ClickEventArgs : EventArgs
    {
        public System.Windows.Point ClickPoint { get; set; }
        public ClickEventArgs(Point P)
        {
            ClickPoint = P;
        }
    }
    public delegate void SelectRectangleHandler(object sender, SelectRectangleArgs e);
    public class SelectRectangleArgs : EventArgs
    {
        public RotatedRect RectangleSelected { get; set; }
        public SelectRectangleArgs(RotatedRect R)
        {
            RectangleSelected = R;
        }
    }

    public interface IImageboxViewModel
    {
        void UpdateSelectedRect(RotatedRect rect);

        void UpdateIsSelectedRectState(bool state);

        void UpdateMouseInfor(int x, int y, int A, int R, int G, int B);

    }
}
