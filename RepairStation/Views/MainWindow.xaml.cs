using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AI_AOI.Config;
using AI_AOI.Database;
using AI_AOI.Utils;
using AIOT.Utils;
using Andrew.Controls;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Newtonsoft.Json.Linq;
using NLog;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace AI_AOI.Views {
    public partial class MainWindow : Window
    {
        readonly NLog.Logger Logger = NLog.LogManager.GetLogger("debug");

        private Timer StatisticsPollTimer;

        int CurrentComponentLocation = 0;

        Image<Bgr, byte> PanelImage;

        DisplayInfor CurrentDisplayInfor = null;
        List<bool?> CurrentConfirmResults = new List<bool?>();
        List<string> CurrentConfirmDefectTypes = new List<string>();
        List<string> CurrentConfirmDisplayTypes = new List<string>();
        string LastSelectedDefectType = "-";
        string LastSelectedDisplayType = "-";
        readonly string AlarmTypesConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "AlarmTypes.json");
        bool IsAlarmTypesConfigMissingNotified = false;
        int AlarmTypePageIndex = 0;
        const int AlarmTypeButtonsPerPage = 9;
        static readonly string[] NumpadShortcutOrder = { "7", "8", "9", "4", "5", "6", "1", "2", "3" };
        bool IsTopComponentImageMode = false;
        HashSet<string> CurrentComponentAlarmTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool IsConfirmingIssue = false;
        bool IsStatisticsSelectionHandlingEnabled = true;
        bool WasStatisticsEmpty = true;
        readonly OperationView OperationScreen = new OperationView();
        readonly StatisticsView StatisticsScreen = new StatisticsView();
        readonly ResultView ResultScreen = new ResultView();
        List<InspectionStatisticRow> StatisticsRows = new List<InspectionStatisticRow>();
        readonly SessionStatistics SessionStats = new SessionStatistics();
        readonly HashSet<Guid> SessionCountedInspectionIds = new HashSet<Guid>();
        readonly Dictionary<string, int> RepeatedComponentRuntimeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public MainWindow()
        {
            InitializeComponent();
            MainScreenHost.Content = StatisticsScreen;
            dgStatistics.SelectionChanged += dgStatistics_SelectionChanged;
            btnSearch.Click += btnSearch_Click;
            btnApplyFilter.Click += btnApplyFilter_Click;
            tbResultStatus.Click += ResultStatusButton_Click;
            PanelImageView.ShowBulkConfirmMenu = true;
            PanelImageView.BulkAllOkRequested += PanelImageView_BulkAllOkRequested;
            PanelImageView.BulkAllNgRequested += PanelImageView_BulkAllNgRequested;
            PanelImageView.Clicked += PanelImageView_Clicked;
            ResultPreviewImage.Clicked += ResultPreviewImage_Clicked;
            tbStatus.Text = "Result: WAITTING";
            ComponentReferenceImageView.MouseLeftButtonUp += ComponentPreviewImage_MouseLeftButtonUp;
            ComponentImageView.MouseLeftButtonUp += ComponentPreviewImage_MouseLeftButtonUp;
            AlarmComponentImageView.MouseLeftButtonUp += ComponentPreviewImage_MouseLeftButtonUp;

            Task.Run(() => {
                if (SQL.IsDatabaseConnected()) {
                    Logger.Info("Connected database");
                    Application.Current.Dispatcher.Invoke(new Action(() => {
                        elDatabaseStatus.Fill = Brushes.Green;
                    }));
                }
            });

            StatisticsPollTimer = new System.Timers.Timer(2000);
            StatisticsPollTimer.Elapsed += StatisticsPollTimer_Elapsed;
            StatisticsPollTimer.AutoReset = true;
            StatisticsPollTimer.Enabled = false;
        }

        private TextBlock tbAlarmedStats => OperationScreen.AlarmedStatsText;
        private TextBlock tbHeaderBoardInfo => OperationScreen.HeaderBoardInfoText;
        private Andrew.Controls.ImageBox PanelImageView => OperationScreen.PanelImage;
        private System.Windows.Controls.Image ComponentImageView => OperationScreen.ComponentImage;
        private System.Windows.Controls.Image ComponentReferenceImageView => OperationScreen.ComponentReferenceImage;
        private System.Windows.Controls.Image AlarmComponentImageView => OperationScreen.AlarmComponentImage;
        private Grid gButtons => OperationScreen.AlarmButtonsGrid;
        private Andrew.Controls.ImageBox ResultPreviewImage => ResultScreen.PreviewImage;
        private TextBlock tbResultInformation => ResultScreen.InformationText;
        private TextBlock tbResultRemain => ResultScreen.RemainText;
        private TextBlock tbResultStatistics => ResultScreen.StatisticsText;
        private Button tbResultStatus => ResultScreen.StatusText;
        private DataGrid dgStatistics => StatisticsScreen.StatisticsGrid;
        private TextBox tbSearchBarcode => StatisticsScreen.SearchBarcodeTextBox;
        private TextBox tbFilterBoard => StatisticsScreen.FilterBoardTextBox;
        private TextBox tbFilterLine => StatisticsScreen.FilterLineTextBox;
        private TextBox tbFilterProductLot => StatisticsScreen.FilterProductLotTextBox;
        private TextBox tbFilterStation => StatisticsScreen.FilterStationTextBox;
        private Button btnSearch => StatisticsScreen.SearchButton;
        private Button btnApplyFilter => StatisticsScreen.ApplyFilterButton;
        private TextBlock tbQtyInspectedPCBs => StatisticsScreen.QtyInspectedPcbsText;
        private TextBlock tbQtyOfNgPCBs => StatisticsScreen.QtyNgPcbsText;
        private TextBlock tbNgPcbRate => StatisticsScreen.NgPcbRateText;
        private TextBlock tbQtyInspectedComponents => StatisticsScreen.QtyInspectedComponentsText;
        private TextBlock tbQtyOfNgAoiComponents => StatisticsScreen.QtyNgAoiComponentsText;
        private TextBlock tbQtyOfNgComponents => StatisticsScreen.QtyNgComponentsText;
        private TextBlock tbNgComponentRate => StatisticsScreen.NgComponentRateText;
        private TextBlock tbNgAoiComponentRate => StatisticsScreen.NgAoiComponentRateText;
        private TextBlock tbIpy => StatisticsScreen.IpyText;
        private TextBlock tbFpy => StatisticsScreen.FpyText;

        private void LoadComponentLocation(int ComponentLocation)
        {
            if (PanelImage is null) return;
            RefreshPanelOverlayColors(ComponentLocation);

            var CurrentComponentInfor = CurrentDisplayInfor.ComponentInfors[ComponentLocation];
            CurrentComponentAlarmTypes = new HashSet<string>(
                (CurrentComponentInfor.AlarmTypes ?? new List<string>())
                    .Select(x => (x ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);
            var catalogText = string.IsNullOrWhiteSpace(CurrentComponentInfor.Catalog)
                ? "Catalog"
                : CurrentComponentInfor.Catalog;
            tbHeaderBoardInfo.Text = $"{CurrentComponentInfor.Name} @ {CurrentComponentInfor.BlockID} {catalogText}";
            UpdateCurrentBoardNameText(CurrentDisplayInfor);

            // Default on each component is Side view.
            IsTopComponentImageMode = false;
            RenderComponentPreviewImages(CurrentComponentInfor);
            AlarmTypePageIndex = 0;
            UpdateAlarmTypeButtons(CurrentComponentInfor, keepCurrentPage: false);
            UpdateMainStatusForCurrentComponent();
        }

        private void ComponentPreviewImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (CurrentDisplayInfor == null || CurrentDisplayInfor.ComponentInfors == null) return;
            if (CurrentComponentLocation < 0 || CurrentComponentLocation >= CurrentDisplayInfor.ComponentInfors.Count) return;

            IsTopComponentImageMode = !IsTopComponentImageMode;
            RenderComponentPreviewImages(CurrentDisplayInfor.ComponentInfors[CurrentComponentLocation]);
            e.Handled = true;
        }

        private void PanelImageView_Clicked(object sender, ClickEventArgs e)
        {
            JumpToComponentAtPanelPoint(e?.ClickPoint, returnToOperationView: false);
        }

        private void ResultPreviewImage_Clicked(object sender, ClickEventArgs e)
        {
            JumpToComponentAtPanelPoint(e?.ClickPoint, returnToOperationView: true);
        }

        private void JumpToComponentAtPanelPoint(System.Windows.Point? clickPoint, bool returnToOperationView)
        {
            if (!clickPoint.HasValue) return;
            if (CurrentDisplayInfor?.ComponentInfors == null || CurrentDisplayInfor.ComponentInfors.Count == 0) return;

            int componentIndex = FindComponentIndexAtPanelPoint(clickPoint.Value);
            if (componentIndex < 0) return;

            if (returnToOperationView)
            {
                MainScreenHost.Content = OperationScreen;
                IsConfirmingIssue = true;
            }

            if (!IsConfirmingIssue) return;

            CurrentComponentLocation = componentIndex;
            LoadComponentLocation(CurrentComponentLocation);
            RefreshConfirmProgress();
        }

        private int FindComponentIndexAtPanelPoint(System.Windows.Point point)
        {
            var rectangles = CurrentDisplayInfor?.MyDrawedRectangle;
            if (rectangles == null || rectangles.Count == 0) return -1;

            int count = Math.Min(rectangles.Count, CurrentDisplayInfor.ComponentInfors?.Count ?? 0);
            for (int i = count - 1; i >= 0; i--)
            {
                if (IsPointInsideDrawedRectangle(point, rectangles[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsPointInsideDrawedRectangle(System.Windows.Point point, MyDrawedRectangle rectangle)
        {
            if (rectangle == null) return false;
            if (rectangle.OriginWidth <= 0 || rectangle.OriginHeight <= 0) return false;

            double dx = point.X - rectangle.OriginX;
            double dy = point.Y - rectangle.OriginY;
            double radians = rectangle.OriginAngle * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            double localX = dx * cos - dy * sin;
            double localY = dx * sin + dy * cos;

            return Math.Abs(localX) <= rectangle.OriginWidth / 2.0 &&
                   Math.Abs(localY) <= rectangle.OriginHeight / 2.0;
        }

        private void RenderComponentPreviewImages(ComponentInfor componentInfor)
        {
            if (componentInfor == null)
            {
                ComponentReferenceImageView.Source = null;
                ComponentImageView.Source = null;
                AlarmComponentImageView.Source = null;
                return;
            }

            byte[] refBytes = IsTopComponentImageMode && componentInfor.TopReferenceImageBytes != null
                ? componentInfor.TopReferenceImageBytes
                : componentInfor.SideReferenceImageBytes;
            byte[] componentBytes = IsTopComponentImageMode && componentInfor.TopImageBytes != null
                ? componentInfor.TopImageBytes
                : componentInfor.SideImageBytes;
            byte[] alarmBytes = IsTopComponentImageMode && componentInfor.AlarmTopImageBytes != null
                ? componentInfor.AlarmTopImageBytes
                : componentInfor.AlarmSideImageBytes;

            ComponentReferenceImageView.Source = ScaleImageSourceIfSmallerThanSize(
                CreateImageSourceFromBytes(refBytes),
                2.0,
                256);
            ComponentReferenceImageView.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            ComponentReferenceImageView.RenderTransform = new System.Windows.Media.RotateTransform(componentInfor.Angle + 180);
            ComponentImageView.Source = CreateImageSourceFromBytes(componentBytes);
            ComponentImageView.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            ComponentImageView.RenderTransform = new System.Windows.Media.RotateTransform(180);
            AlarmComponentImageView.Source = CreateImageSourceFromBytes(alarmBytes);
        }

        private void DisplayProcess()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ResetConfirmIssueUI();
                    UpdateDisplayHeader(CurrentDisplayInfor);

                    PanelImage?.Dispose();
                    PanelImage = null;

                    // GC.Collect();
                    // GC.WaitForPendingFinalizers();
                    // GC.Collect();

                    if (CurrentDisplayInfor.PanelImage != null && !CurrentDisplayInfor.PanelImage.IsEmpty)
                    {
                        PanelImage = CurrentDisplayInfor.PanelImage.ToImage<Bgr, byte>();
                    }

                    if (PanelImage != null)
                    {
                        var bitmap = PanelImage.Resize(SoftwareSettingsManager.Current.ImageScale, Emgu.CV.CvEnum.Inter.Linear).ToBitmap();
                        PanelImageView.ImageSource = Convertor.Bitmap2BitmapSource(bitmap);
                        bitmap?.Dispose();
                    }
                    else
                    {
                        PanelImageView.ImageSource = null;
                    }
                    EnsureComponentOverlays(CurrentDisplayInfor);
                    PanelImageView.DrawedRectangles = CurrentDisplayInfor.MyDrawedRectangle;
                    PanelImageView.DrawedTexts = CurrentDisplayInfor.MyDrawedText;
                    if (CurrentDisplayInfor.Status)
                    {
                        IsConfirmingIssue = false;
                        CurrentConfirmResults = new List<bool?>();
                        CurrentConfirmDefectTypes = new List<string>();
                        CurrentConfirmDisplayTypes = new List<string>();
                        IsTopComponentImageMode = false;
                        ComponentReferenceImageView.Source = null;
                        ComponentReferenceImageView.RenderTransform = null;
                        ComponentImageView.Source = null;
                        AlarmComponentImageView.Source = null;
                        UpdateAlarmedStatistics();
                        ShowResult("OK");
                    }
                    else
                    {
                        if (CurrentDisplayInfor is null) return;
                        if (CurrentDisplayInfor.ComponentInfors.Count <= 0)
                        {
                            FinalizeConfirmIssue();
                            return;
                        }

                        IsConfirmingIssue = true;
                        CurrentConfirmResults = Enumerable.Repeat<bool?>(null, CurrentDisplayInfor.ComponentInfors.Count).ToList();
                        CurrentConfirmDefectTypes = Enumerable.Repeat<string>(null, CurrentDisplayInfor.ComponentInfors.Count).ToList();
                        CurrentConfirmDisplayTypes = Enumerable.Repeat<string>(null, CurrentDisplayInfor.ComponentInfors.Count).ToList();
                        CurrentComponentLocation = 0;
                        LoadComponentLocation(CurrentComponentLocation);
                        RefreshConfirmProgress();
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private void ResetConfirmIssueUI()
        {
            IsConfirmingIssue = false;
            CurrentConfirmResults = new List<bool?>();
            CurrentConfirmDefectTypes = new List<string>();
            CurrentConfirmDisplayTypes = new List<string>();
            LastSelectedDefectType = "-";
            LastSelectedDisplayType = "-";
            tbStatus.Text = "Selected: -";
            UpdateCurrentBoardNameText(null);
            if (gButtons != null)
            {
                gButtons.Children.Clear();
                gButtons.RowDefinitions.Clear();
                gButtons.ColumnDefinitions.Clear();
            }
        }

        private void dgStatistics_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsStatisticsSelectionHandlingEnabled) return;
            if (!(dgStatistics.SelectedItem is InspectionStatisticRow row)) return;
            ProcessSelectedStatisticsInspection(row);
        }

        private void LoadStatisticsData()
        {
            try
            {
                StatisticsRows = Query.GetInspectionStatistics(
                    string.Empty,
                    null,
                    null,
                    tbSearchBarcode.Text?.Trim(),
                    0);

                ApplyStatisticsClientFilter();
            }
            catch (Exception ex)
            {
                tbStatus.Text = $"Error: {ex.Message}";
            }
        }

        private void ApplyStatisticsClientFilter()
        {
            IEnumerable<InspectionStatisticRow> filtered = StatisticsRows ?? Enumerable.Empty<InspectionStatisticRow>();

            string board = (tbFilterBoard.Text ?? string.Empty).Trim();
            string line = (tbFilterLine.Text ?? string.Empty).Trim();
            string lot = (tbFilterProductLot.Text ?? string.Empty).Trim();
            string station = (tbFilterStation.Text ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(line))
                filtered = filtered.Where(x => (x.Line ?? string.Empty).IndexOf(line, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!string.IsNullOrWhiteSpace(board))
                filtered = filtered.Where(x => (x.BoardName ?? string.Empty).IndexOf(board, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!string.IsNullOrWhiteSpace(lot))
                filtered = filtered.Where(x => (x.ProductLot ?? string.Empty).IndexOf(lot, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!string.IsNullOrWhiteSpace(station))
                filtered = filtered.Where(x => (x.Station ?? string.Empty).IndexOf(station, StringComparison.OrdinalIgnoreCase) >= 0);

            var list = filtered.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                list[i].No = i + 1;
            }

            dgStatistics.ItemsSource = list;
            UpdateStatisticsSummary();
            WasStatisticsEmpty = list.Count == 0;
        }

        private void UpdateStatisticsSummary()
        {
            int inspectedPcbs = SessionStats.InspectedPcbs;
            int ngPcbs = SessionStats.NgPcbs;
            int inspectedComponents = SessionStats.InspectedComponents;
            int ngAoiComponents = SessionStats.NgAoiComponents;
            int ngComponents = SessionStats.NgComponents;
            int aoiDirectPassPcbs = SessionStats.AoiDirectPassPcbs;

            double fpy = inspectedPcbs == 0 ? 0 : (inspectedPcbs - ngPcbs) * 100.0 / inspectedPcbs;
            double ipy = inspectedPcbs == 0 ? 0 : aoiDirectPassPcbs * 100.0 / inspectedPcbs;
            double ngPcbRate = inspectedPcbs == 0 ? 0 : ngPcbs * 100.0 / inspectedPcbs;
            double ngComponentRate = inspectedComponents == 0 ? 0 : ngComponents * 100.0 / inspectedComponents;
            double ngAoiComponentRate = inspectedComponents == 0 ? 0 : ngAoiComponents * 100.0 / inspectedComponents;

            tbQtyInspectedPCBs.Text = $"Qty. Of Inspected PCBs \t\t{inspectedPcbs}";
            tbQtyOfNgPCBs.Text = $"Qty. Of NG PCBs \t\t\t{ngPcbs}";
            tbNgPcbRate.Text = $"NG PCB Rate \t\t\t{ngPcbRate:0.00}%";
            tbQtyInspectedComponents.Text = $"Qty. Of Inspected Components \t{inspectedComponents}";
            tbQtyOfNgAoiComponents.Text = $"Qty. Of NG AOI Components \t{ngAoiComponents}";
            tbQtyOfNgComponents.Text = $"Qty. Of NG Components \t\t{ngComponents}";
            tbNgComponentRate.Text = $"NG Component Rate \t\t{ngComponentRate:0.00}%";
            tbNgAoiComponentRate.Text = $"NG AOI Component Rate \t\t{ngAoiComponentRate:0.00}%";
            tbIpy.Text = $"IPY \t\t\t\t{ipy:0.00}%";
            tbFpy.Text = $"FPY \t\t\t\t{fpy:0.00}%";
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            LoadStatisticsData();
        }

        private void btnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyStatisticsClientFilter();
        }

        private DisplayInfor BuildDisplayInfor(QueryResult queryResult, InspectionStatisticRow rowMeta)
        {
            Mat panelImage = null;
            double imageWidthPx = 0;
            double imageHeightPx = 0;

            if (queryResult?.BoardImageBytes != null && queryResult.BoardImageBytes.Length > 0)
            {
                try
                {
                    panelImage = new Mat();
                    CvInvoke.Imdecode(queryResult.BoardImageBytes, Emgu.CV.CvEnum.ImreadModes.Color, panelImage);
                    if (panelImage != null && !panelImage.IsEmpty)
                    {
                        CvInvoke.Rotate(panelImage, panelImage, RotateFlags.Rotate180);
                        imageWidthPx = panelImage.Cols;
                        imageHeightPx = panelImage.Rows;
                    }
                    else
                    {
                        panelImage?.Dispose();
                        panelImage = null;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to decode BoardImage for inspection {0}. Continue without panel image.", queryResult?.ID);
                    panelImage?.Dispose();
                    panelImage = null;
                }
            }

            double boardWidthMm = Math.Max(queryResult.BoardWidth, 0.000001);
            double boardHeightMm = Math.Max(queryResult.BoardHeight, 0.000001);
            double scaleX = imageWidthPx / boardWidthMm;
            double scaleY = imageHeightPx / boardHeightMm;

            var displayInfor = new DisplayInfor
            {
                InspectionID = queryResult.ID,
                SN = queryResult.SN,
                PanelImage = panelImage,
                Model = queryResult.BoardName,
                Status = queryResult.Status,
                InspectTime = queryResult.Time,
                HasMark = queryResult.HasMark,
                RailID = queryResult.RailID,
                Station = queryResult.Station,
                ProductLot = queryResult.ProductLot,
                Line = queryResult.Line,
                TotalComponentCount = rowMeta?.TotalComponentCount ?? 0,
                BlockCount = rowMeta?.BlockCount ?? 0,
                BlockNumbers = queryResult.BlockNumbers?.Distinct().OrderBy(x => x).ToList() ?? new List<int>(),
                NgAoiComponentCount = rowMeta?.AlarmComponentCount ?? (queryResult.DefectLocations?.Count ?? 0)
            };

            foreach (var defect in queryResult.DefectLocations ?? Enumerable.Empty<DefectLocation>())
            {
                // mm -> pixel
                double x = (boardWidthMm-defect.X) * scaleX;
                double y = (boardHeightMm-defect.Y) * scaleY;
                double w = Math.Abs(defect.Width * scaleX);
                double h = Math.Abs(defect.Height * scaleY);

                displayInfor.ComponentInfors.Add(new ComponentInfor
                {
                    ComponentID = defect.ComponentID,
                    Name = defect.Name,
                    Catalog = defect.Catalog,
                    BlockID = defect.Block,
                    X = x,
                    Y = y,
                    ComponentX = defect.X,
                    ComponentY = defect.Y,
                    Width = w,
                    Height = h,
                    Angle = defect.Angle,
                    TopImageBytes = defect.TopImageBytes,
                    SideImageBytes = defect.SideImageBytes,
                    TopReferenceImageBytes = defect.TopReferenceImageBytes,
                    SideReferenceImageBytes = defect.SideReferenceImageBytes,
                    AlarmTopImageBytes = defect.AlarmTopImageBytes,
                    AlarmSideImageBytes = defect.AlarmSideImageBytes,
                    AlarmInfors = defect.AlarmInfors?.ToList() ?? new List<AlarmImageInfo>(),
                    AlarmTypes = defect.AlarmTypes?.Distinct().ToList() ?? new List<string>()
                });

            }

            EnsureComponentOverlays(displayInfor);
            return displayInfor;
        }

        private void EnsureComponentOverlays(DisplayInfor displayInfor)
        {
            if (displayInfor == null) return;

            displayInfor.MyDrawedRectangle.Clear();
            displayInfor.MyDrawedText.Clear();

            foreach (var component in displayInfor.ComponentInfors)
            {
                displayInfor.MyDrawedRectangle.Add(new MyDrawedRectangle(
                    component.X * SoftwareSettingsManager.Current.ImageScale,
                    component.Y * SoftwareSettingsManager.Current.ImageScale,
                    component.Width * SoftwareSettingsManager.Current.ImageScale,
                    component.Height * SoftwareSettingsManager.Current.ImageScale,
                    component.Angle,
                    Brushes.Orange,
                    SoftwareSettingsManager.Current.RectangleThickness));

                displayInfor.MyDrawedText.Add(new MyDrawedText(
                    component.Name+"@"+component.BlockID,
                    component.X * SoftwareSettingsManager.Current.ImageScale,
                    component.Y * SoftwareSettingsManager.Current.ImageScale,
                    Brushes.Orange,
                    SoftwareSettingsManager.Current.FontSize));
            }
        }

        private void RefreshConfirmProgress()
        {

            int confirmedCount = CurrentConfirmResults.Count(x => x.HasValue);
            int totalCount = CurrentConfirmResults.Count;
            UpdateAlarmedStatistics();
        }

        private int FindNextUnconfirmedIndex(int currentIndex)
        {
            if (CurrentConfirmResults.Count == 0) return -1;

            for (int offset = 1; offset <= CurrentConfirmResults.Count; offset++)
            {
                int idx = (currentIndex + offset) % CurrentConfirmResults.Count;
                if (!CurrentConfirmResults[idx].HasValue) return idx;
            }

            return -1;
        }

        private void ConfirmCurrentIssue(bool hasIssue, string defectType, string displayType)
        {
            if (!IsConfirmingIssue || CurrentDisplayInfor == null) return;
            if (CurrentComponentLocation < 0 || CurrentComponentLocation >= CurrentConfirmResults.Count) return;

            string selectedDefect = string.IsNullOrWhiteSpace(defectType)
                ? (hasIssue ? "NG" : "OK")
                : defectType.Trim();
            string selectedDisplay = string.IsNullOrWhiteSpace(displayType)
                ? selectedDefect
                : displayType.Trim();
            CurrentConfirmResults[CurrentComponentLocation] = hasIssue;
            if (CurrentComponentLocation < CurrentConfirmDefectTypes.Count)
            {
                CurrentConfirmDefectTypes[CurrentComponentLocation] = selectedDefect;
            }
            if (CurrentComponentLocation < CurrentConfirmDisplayTypes.Count)
            {
                CurrentConfirmDisplayTypes[CurrentComponentLocation] = selectedDisplay;
            }
            LastSelectedDefectType = selectedDefect;
            LastSelectedDisplayType = selectedDisplay;
            tbStatus.Text = $"Selected: {selectedDisplay}";
            RefreshConfirmProgress();

            int nextIndex = FindNextUnconfirmedIndex(CurrentComponentLocation);
            if (nextIndex >= 0)
            {
                CurrentComponentLocation = nextIndex;
                LoadComponentLocation(CurrentComponentLocation);
            }
            else
            {
                FinalizeConfirmIssue();
            }
        }

        private void UpdateMainStatusForCurrentComponent()
        {
            if (!IsConfirmingIssue || CurrentDisplayInfor == null)
            {
                return;
            }

            if (CurrentComponentLocation < 0 || CurrentComponentLocation >= CurrentConfirmDefectTypes.Count)
            {
                tbStatus.Text = $"Selected: {LastSelectedDisplayType}";
                return;
            }

            var selected = CurrentConfirmDisplayTypes.ElementAtOrDefault(CurrentComponentLocation);
            tbStatus.Text = string.IsNullOrWhiteSpace(selected)
                ? $"Selected: {LastSelectedDisplayType}"
                : $"Selected: {selected}";
        }

        private System.Windows.Media.Brush ResolveComponentStroke(int componentIndex, bool isCurrent)
        {
            if (isCurrent) return Brushes.Yellow;
            if (componentIndex < 0 || componentIndex >= CurrentConfirmResults.Count) return Brushes.Orange;

            var result = CurrentConfirmResults[componentIndex];
            if (!result.HasValue) return Brushes.Orange;
            return result.Value ? Brushes.Red : Brushes.LimeGreen;
        }

        private void RefreshPanelOverlayColors(int highlightedComponentIndex)
        {
            if (PanelImageView?.DrawedRectangles == null || PanelImageView.DrawedTexts == null) return;

            int count = Math.Min(PanelImageView.DrawedRectangles.Count, PanelImageView.DrawedTexts.Count);
            for (int loc = 0; loc < count; loc++)
            {
                bool isCurrent = loc == highlightedComponentIndex;
                var stroke = ResolveComponentStroke(loc, isCurrent);
                PanelImageView.DrawedRectangles[loc].DrawedRectangle.Stroke = stroke;
                PanelImageView.DrawedRectangles[loc].SubDrawedRectangle.Stroke = stroke;
                PanelImageView.DrawedTexts[loc].TextBlock.Foreground = stroke;
            }
        }

        private void FinalizeConfirmIssue()
        {
            bool hasAnyIssue = CurrentConfirmResults.Any(x => x == true);
            RefreshPanelOverlayColors(-1);
            if (hasAnyIssue)
            {
                ShowResult("NG");
            }
            else
            {
                ShowResult("OK");
            }

            IsConfirmingIssue = false;
            UpdateAlarmedStatistics();
        }

        private void ShowResult(string text)
        {
            var key = (text ?? string.Empty).Trim().ToUpperInvariant();
            tbStatus.Text = $"Result: {key}";

            ResultPreviewImage.ImageSource = PanelImageView?.ImageSource;
            ResultPreviewImage.DrawedRectangles = CloneDrawedRectangles(PanelImageView?.DrawedRectangles);
            ResultPreviewImage.DrawedTexts = CloneDrawedTexts(PanelImageView?.DrawedTexts);
            ApplyAlarmedStatsText(tbResultInformation);
            tbResultRemain.Text = "Statistics";
            tbResultStatistics.Text = BuildResultStatisticsText();
            tbResultStatus.Content = key;
            tbResultStatus.Cursor = Cursors.Hand;
            tbResultStatus.ToolTip = "Click to confirm and open next inspection";

            if (key == "OK")
            {
                //bdResultStatus.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(95, 95, 95));
                tbResultStatus.Foreground = Brushes.Green;
            }
            else if (key == "NG")
            {
                //bdResultStatus.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(186, 186, 186));
                tbResultStatus.Foreground = Brushes.Red;
            }
            else
            {
                //bdResultStatus.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(186, 186, 186));
                tbResultStatus.Foreground = Brushes.Black;
            }

            MainScreenHost.Content = ResultScreen;
        }

        private void ResultStatusButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainScreenHost.Content != ResultScreen) return;
            CommitCurrentInspectionAndMoveNext();
        }

        private void PanelImageView_BulkAllOkRequested(object sender, EventArgs e)
        {
            ApplyBulkConfirmForCurrentInspection(false, "OK");
        }

        private void PanelImageView_BulkAllNgRequested(object sender, EventArgs e)
        {
            ApplyBulkConfirmForCurrentInspection(true, "Missing");
        }

        private void ApplyBulkConfirmForCurrentInspection(bool hasIssue, string defectType)
        {
            if (!IsConfirmingIssue || CurrentDisplayInfor == null) return;
            int total = CurrentDisplayInfor.ComponentInfors?.Count ?? 0;
            if (total <= 0) return;

            CurrentConfirmResults = Enumerable.Repeat<bool?>(hasIssue, total).ToList();
            CurrentConfirmDefectTypes = Enumerable.Repeat(defectType, total).ToList();
            CurrentConfirmDisplayTypes = Enumerable.Repeat(defectType, total).ToList();
            LastSelectedDefectType = defectType;
            LastSelectedDisplayType = defectType;
            tbStatus.Text = $"Selected: {defectType}";
            RefreshConfirmProgress();
            FinalizeConfirmIssue();
        }

        private ObservableCollection<MyDrawedRectangle> CloneDrawedRectangles(ObservableCollection<MyDrawedRectangle> source)
        {
            var cloned = new ObservableCollection<MyDrawedRectangle>();
            if (source == null) return cloned;

            foreach (var item in source)
            {
                if (item == null) continue;

                int thickness = (int)Math.Max(1, Math.Round(item.OriginThickness));
                var stroke = item.DrawedRectangle?.Stroke ?? Brushes.Orange;
                var copy = new MyDrawedRectangle(
                    item.OriginX,
                    item.OriginY,
                    item.OriginWidth,
                    item.OriginHeight,
                    item.OriginAngle,
                    stroke,
                    thickness);

                copy.OriginThickness = item.OriginThickness;
                copy.DrawedRectangle.Stroke = item.DrawedRectangle?.Stroke ?? stroke;
                copy.SubDrawedRectangle.Stroke = item.SubDrawedRectangle?.Stroke ?? stroke;
                copy.DrawedRectangle.Fill = item.DrawedRectangle?.Fill;
                copy.SubDrawedRectangle.Fill = item.SubDrawedRectangle?.Fill;

                cloned.Add(copy);
            }

            return cloned;
        }

        private ObservableCollection<MyDrawedText> CloneDrawedTexts(ObservableCollection<MyDrawedText> source)
        {
            var cloned = new ObservableCollection<MyDrawedText>();
            if (source == null) return cloned;

            foreach (var item in source)
            {
                if (item == null) continue;

                var fg = item.TextBlock?.Foreground ?? Brushes.Orange;
                var copy = new MyDrawedText(
                    item.TextBlock?.Text ?? string.Empty,
                    item.OriginX,
                    item.OriginY,
                    fg,
                    item.OriginFontSize);

                cloned.Add(copy);
            }

            return cloned;
        }

        private string BuildResultStatisticsText()
        {
            if (CurrentDisplayInfor == null)
            {
                return "Board Name\nInspected Date\nBarcode\nProduct Lots\nLine\nStation\nBlock Count 0 / 0\nComponent Count 0 / 0 / 0";
            }

            int ngComponentCount = CurrentConfirmResults.Count(x => x == true);
            int okComponentCount = CurrentConfirmResults.Count(x => x == false);
            int totalComponentCount = CurrentDisplayInfor.ComponentInfors?.Count ?? 0;
            int blockCount = (CurrentDisplayInfor.ComponentInfors ?? new List<ComponentInfor>())
                .Select(c => c.BlockID)
                .Distinct()
                .Count();

            return
                $"Board Name \t\t{CurrentDisplayInfor.Model}\n" +
                $"Inspected Date \t\t{CurrentDisplayInfor.InspectTime:yyyy/MM/dd HH:mm:ss}\n" +
                $"Barcode \t\t\t{CurrentDisplayInfor.SN}\n" +
                $"Product Lots \t\t{CurrentDisplayInfor.ProductLot}\n" +
                $"Line \t\t\t{CurrentDisplayInfor.Line}\n" +
                $"Station \t\t\t{CurrentDisplayInfor.Station}\n" +
                $"Block Count \t\t{blockCount} / {blockCount}\n" +
                $"Component Count \t{ngComponentCount} / {okComponentCount} / {totalComponentCount}";
        }

        private void UpdateDisplayHeader(DisplayInfor displayInfor)
        {
            if (displayInfor == null)
            {
                tbHeaderBoardInfo.Text = "Catalog";
                tbAlarmedStats.Text = "Alarmed Component Statistics 0 / 0 / 0 / 0";
                UpdateCurrentBoardNameText(null);
                return;
            }

            tbHeaderBoardInfo.Text = "Catalog";
            UpdateCurrentBoardNameText(displayInfor);
            UpdateAlarmedStatistics();
        }

        private void UpdateCurrentBoardNameText(DisplayInfor displayInfor)
        {
            if (tbCurrentBoardName == null) return;

            tbCurrentBoardName.Text = displayInfor == null
                ? string.Empty
                : displayInfor.Model ?? string.Empty;
        }

        private void UpdateAlarmedStatistics()
        {
            ApplyAlarmedStatsText(tbAlarmedStats);
        }

        private void ApplyAlarmedStatsText(TextBlock target)
        {
            if (target == null) return;

            int total = CurrentDisplayInfor?.ComponentInfors?.Count ?? 0;
            int ng = CurrentConfirmResults.Count(x => x == true);
            int ok = CurrentConfirmResults.Count(x => x == false);
            int pending = Math.Max(total - ng - ok, 0);
            string boardInfo = CurrentDisplayInfor == null
                ? string.Empty
                : $"  Barcode {CurrentDisplayInfor.SN}  Rail {CurrentDisplayInfor.RailID}  Line {CurrentDisplayInfor.Line}";

            target.Inlines.Clear();
            target.Inlines.Add(new Run("Alarmed Component Statistics "));
            target.Inlines.Add(new Run(ng.ToString()) { Foreground = Brushes.Red });
            target.Inlines.Add(new Run(" / "));
            target.Inlines.Add(new Run(ok.ToString()) { Foreground = Brushes.LimeGreen });
            target.Inlines.Add(new Run(" / "));
            target.Inlines.Add(new Run(pending.ToString()) { Foreground = Brushes.Yellow });
            target.Inlines.Add(new Run($" / {total}{boardInfo}"));
        }

        private void UpdateAlarmTypeButtons(ComponentInfor componentInfor, bool keepCurrentPage)
        {
            if (gButtons == null) return;

            gButtons.Children.Clear();
            gButtons.RowDefinitions.Clear();
            gButtons.ColumnDefinitions.Clear();

            var alarmTypes = LoadAlarmTypeOptionsFromJson();
            var fixedAlarmTypes = alarmTypes
                .Where(a => !string.IsNullOrWhiteSpace(a?.Original))
                .ToList();

            int pageCount = Math.Max(1, (int)Math.Ceiling(fixedAlarmTypes.Count / (double)AlarmTypeButtonsPerPage));
            if (!keepCurrentPage)
            {
                AlarmTypePageIndex = 0;
            }
            if (AlarmTypePageIndex < 0 || AlarmTypePageIndex >= pageCount)
            {
                AlarmTypePageIndex = 0;
            }

            for (int i = 0; i < 3; i++)
            {
                gButtons.ColumnDefinitions.Add(new ColumnDefinition());
            }

            int buttonIndex = 0;
            AddConfirmButton("OK", "OK", buttonIndex++, false, GetConfirmOkShortcutDisplayText());

            // Add quick button for current component alarm type from Alarm table, right after OK.
            var currentComponentAlarmType = (componentInfor?.AlarmTypes ?? new List<string>())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
            if (!string.IsNullOrWhiteSpace(currentComponentAlarmType))
            {
                var mappedDisplay = fixedAlarmTypes
                    .FirstOrDefault(a => string.Equals((a?.Original ?? string.Empty).Trim(), currentComponentAlarmType, StringComparison.OrdinalIgnoreCase))
                    ?.Display;
                var minusCaption = string.IsNullOrWhiteSpace(mappedDisplay)
                    ? currentComponentAlarmType
                    : mappedDisplay.Trim();

                // Caption follows language in AlarmTypes.json, while DefectType keeps original raw value.
                AddConfirmButton(minusCaption, currentComponentAlarmType, buttonIndex++, true, "-");

                // Keep one blank slot after '-' button.
                buttonIndex++;
            }

            int pageStart = AlarmTypePageIndex * AlarmTypeButtonsPerPage;
            var pageAlarmTypes = fixedAlarmTypes
                .Skip(pageStart)
                .Take(AlarmTypeButtonsPerPage)
                .ToList();
            for (int i = 0; i < pageAlarmTypes.Count && i < NumpadShortcutOrder.Length; i++)
            {
                var alarmType = pageAlarmTypes[i];
                string shortcut = NumpadShortcutOrder[i];
                AddConfirmButton(alarmType.Display, alarmType.Original, buttonIndex++, true, shortcut);
            }

            AddNextPageButton(buttonIndex, pageCount, componentInfor);
        }

        private void AddNextPageButton(int index, int pageCount, ComponentInfor componentInfor)
        {
            int colCount = 3;
            // Always place Next on a new row to avoid overlapping the last alarm buttons
            // when the final page has fewer than 3 items.
            int row = (index + colCount - 1) / colCount;
            while (gButtons.RowDefinitions.Count <= row)
            {
                gButtons.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var nextButton = new Button
            {
                Content = BuildConfirmButtonContent($"Next ({AlarmTypePageIndex + 1}/{Math.Max(1, pageCount)})", "."),
                Margin = new Thickness(1),
                MinHeight = 42,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(62, 92, 120)),
                Foreground = Brushes.White,
                IsEnabled = pageCount > 1
            };
            nextButton.ToolTip = "Shortcut: .";

            nextButton.Click += (s, e) =>
            {
                if (pageCount <= 1) return;
                AlarmTypePageIndex = (AlarmTypePageIndex + 1) % pageCount;
                UpdateAlarmTypeButtons(componentInfor, keepCurrentPage: true);
            };

            Grid.SetRow(nextButton, row);
            Grid.SetColumn(nextButton, 0);
            Grid.SetColumnSpan(nextButton, 3);
            gButtons.Children.Add(nextButton);
        }

        private bool TryNextAlarmTypePageByShortcut()
        {
            if (!IsConfirmingIssue) return false;
            if (MainScreenHost.Content != OperationScreen) return false;
            if (CurrentDisplayInfor == null || CurrentDisplayInfor.ComponentInfors.Count <= 0) return false;
            if (CurrentComponentLocation < 0 || CurrentComponentLocation >= CurrentDisplayInfor.ComponentInfors.Count) return false;

            var alarmTypes = LoadAlarmTypeOptionsFromJson();
            int count = alarmTypes.Count(a => !string.IsNullOrWhiteSpace(a?.Original));
            int pageCount = Math.Max(1, (int)Math.Ceiling(count / (double)AlarmTypeButtonsPerPage));
            if (pageCount <= 1) return false;

            AlarmTypePageIndex = (AlarmTypePageIndex + 1) % pageCount;
            var currentComponentInfor = CurrentDisplayInfor.ComponentInfors[CurrentComponentLocation];
            UpdateAlarmTypeButtons(currentComponentInfor, keepCurrentPage: true);
            return true;
        }

        private List<AlarmTypeOption> LoadAlarmTypeOptionsFromJson()
        {
            try
            {
                if (!File.Exists(AlarmTypesConfigPath))
                {
                    if (!IsAlarmTypesConfigMissingNotified)
                    {
                        IsAlarmTypesConfigMissingNotified = true;
                        UILib.ShowWarning($"Alarm type config not found: {AlarmTypesConfigPath}");
                    }
                    return new List<AlarmTypeOption>();
                }

                var json = File.ReadAllText(AlarmTypesConfigPath);
                var token = JToken.Parse(json);

                if (token is JArray objectArray && objectArray.Count > 0 && objectArray[0] is JObject)
                {
                    var fromObjects = objectArray
                        .OfType<JObject>()
                        .Select(x => new AlarmTypeOption
                        {
                            Original = (x["Original"] ?? x["AlarmTypeOriginal"] ?? x["raw"] ?? x["value"])?.ToString()?.Trim(),
                            Display = (x["Display"] ?? x["AlarmTypeDisplay"] ?? x["label"] ?? x["name"])?.ToString()?.Trim()
                        })
                        .Where(x => !string.IsNullOrWhiteSpace(x.Original) && !string.IsNullOrWhiteSpace(x.Display))
                        .GroupBy(x => x.Display, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .ToList();

                    return fromObjects;
                }

                if (token is JObject obj)
                {
                    // Preferred format: direct key-value map (Original -> Display)
                    // Example:
                    // {
                    //   "Missing": "Thieu thiec",
                    //   "NoPart": "Mat lieu"
                    // }
                    var directMap = obj.Properties()
                        .Where(p => p.Value != null && p.Value.Type == JTokenType.String)
                        .Select(p => new AlarmTypeOption
                        {
                            Original = (p.Name ?? string.Empty).Trim(),
                            Display = (p.Value?.ToString() ?? string.Empty).Trim()
                        })
                        .Where(x => !string.IsNullOrWhiteSpace(x.Original) && !string.IsNullOrWhiteSpace(x.Display))
                        .ToList();
                    if (directMap.Count > 0)
                    {
                        return directMap;
                    }

                    var originalList = ReadStringArray(obj,
                        "AlarmTypesOriginal",
                        "AlarmTypes",
                        "AlarmTypesRaw",
                        "alarms",
                        "Alarms");
                    var displayList = ReadStringArray(obj,
                        "AlarmTypesDisplay",
                        "AlarmDisplay",
                        "AlarmLabels");

                    if (originalList.Count > 0)
                    {
                        var result = new List<AlarmTypeOption>();
                        for (int i = 0; i < originalList.Count; i++)
                        {
                            var original = originalList[i];
                            if (string.IsNullOrWhiteSpace(original)) continue;
                            var display = i < displayList.Count && !string.IsNullOrWhiteSpace(displayList[i])
                                ? displayList[i]
                                : original;
                            result.Add(new AlarmTypeOption
                            {
                                Original = original.Trim(),
                                Display = display.Trim()
                            });
                        }

                        return result
                            .GroupBy(x => x.Display, StringComparer.OrdinalIgnoreCase)
                            .Select(g => g.First())
                            .ToList();
                    }
                }

                if (token is JArray legacyArr)
                {
                    return legacyArr
                        .Select(x => x?.ToString()?.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Select(x => new AlarmTypeOption { Original = x, Display = x })
                        .ToList();
                }

                return new List<AlarmTypeOption>();
            }
            catch (Exception ex)
            {
                UILib.ShowError($"Load AlarmTypes.json failed: {ex.Message}");
                return new List<AlarmTypeOption>();
            }
        }

        private List<string> ReadStringArray(JObject obj, params string[] keys)
        {
            foreach (var key in keys)
            {
                var token = obj[key];
                if (token is JArray arr)
                {
                    return arr
                        .Select(x => x?.ToString())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x.Trim())
                        .ToList();
                }
            }
            return new List<string>();
        }

        private void AddConfirmButton(string caption, string defectType, int index, bool? hasIssue, string shortcut)
        {
            int colCount = 3;
            int row = index / colCount;
            int col = index % colCount;

            while (gButtons.RowDefinitions.Count <= row)
            {
                gButtons.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var button = new Button
            {
                Content = BuildConfirmButtonContent(caption, shortcut),
                Margin = new Thickness(1),
                MinHeight = 48,
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Tag = new ConfirmChoice
                {
                    HasIssue = hasIssue == true,
                    DefectType = hasIssue == true ? (defectType ?? string.Empty).Trim() : "OK",
                    DisplayType = (caption ?? string.Empty).Trim(),
                    Shortcut = shortcut
                }
            };
            if (!string.IsNullOrWhiteSpace(shortcut))
            {
                button.ToolTip = $"Shortcut: {shortcut}";
            }

            if (hasIssue == true)
            {
                button.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 70, 70));
                if (string.Equals(shortcut, "-", StringComparison.Ordinal))
                {
                    // The quick '-' button should always keep default text color.
                    button.Foreground = Brushes.White;
                }
                else if (IsCurrentComponentAlarmType(defectType))
                {
                    button.Foreground = ShouldUseRedAlarmText(defectType) ? Brushes.Red : Brushes.Yellow;
                }
                else
                {
                    // Keep non-current alarm buttons in default color (no red/yellow highlight)
                    button.Foreground = Brushes.White;
                }
            }
            else if (hasIssue == false)
            {
                button.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(95, 95, 95));
                button.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 230, 60));
            }
            else
            {
                button.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 190, 70));
                button.Foreground = Brushes.Black;
            }

            button.Click += ConfirmButton_Click;
            Grid.SetRow(button, row);
            Grid.SetColumn(button, col);
            gButtons.Children.Add(button);
        }

        private bool ShouldUseRedAlarmText(string defectType)
        {
            var type = (defectType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(type)) return false;

            var redListCsv = SoftwareSettingsManager.Current?.AlarmTypesRedText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(redListCsv)) return false;

            var redList = redListCsv
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));

            return redList.Any(x => string.Equals(x, type, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsCurrentComponentAlarmType(string defectType)
        {
            var type = (defectType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(type)) return false;
            return CurrentComponentAlarmTypes != null && CurrentComponentAlarmTypes.Contains(type);
        }

        private object BuildConfirmButtonContent(string caption, string shortcut)
        {
            var root = new Grid();

            var mainText = new TextBlock
            {
                Text = (caption ?? string.Empty).Trim(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };
            root.Children.Add(mainText);

            if (!string.IsNullOrWhiteSpace(shortcut))
            {
                var shortcutText = new TextBlock
                {
                    Text = shortcut,
                    FontSize = 12,
                    FontWeight = FontWeights.Normal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                };
                root.Children.Add(shortcutText);
            }

            return root;
        }

        private bool TryConfirmByShortcut(string shortcut)
        {
            if (!IsConfirmingIssue) return false;
            if (string.IsNullOrWhiteSpace(shortcut)) return false;
            if (gButtons == null) return false;

            var match = gButtons.Children
                .OfType<Button>()
                .Select(b => b.Tag as ConfirmChoice)
                .FirstOrDefault(c => c != null && string.Equals(c.Shortcut, shortcut, StringComparison.OrdinalIgnoreCase));

            if (match == null) return false;
            ConfirmCurrentIssue(match.HasIssue, match.DefectType, match.DisplayType);
            return true;
        }

        private string ResolveShortcutFromKey(Key key)
        {
            if (key == Key.OemMinus || key == Key.Subtract)
            {
                return "-";
            }

            if (key == Key.OemPeriod || key == Key.Decimal)
            {
                return ".";
            }

            if (key >= Key.D1 && key <= Key.D9)
            {
                return ((int)(key - Key.D0)).ToString();
            }

            if (key >= Key.NumPad1 && key <= Key.NumPad9)
            {
                return ((int)(key - Key.NumPad0)).ToString();
            }

            return null;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;

            if (btn.Tag is ConfirmChoice confirmChoice)
            {
                ConfirmCurrentIssue(confirmChoice.HasIssue, confirmChoice.DefectType, confirmChoice.DisplayType);
            }
        }

        private bool OpenInspection(Guid inspectionId)
        {
            QueryResult queryResult;
            try
            {
                queryResult = Query.GetInspectionDetail(inspectionId);
            }
            catch (Exception ex)
            {
                UILib.ShowError($"Load inspection failed: {ex.Message}");
                return false;
            }

            if (queryResult == null)
            {
                UILib.ShowWarning("Can not load selected inspection.");
                return false;
            }

            var rowMeta = StatisticsRows.FirstOrDefault(x => x.InspectionID == inspectionId);
            CurrentDisplayInfor = BuildDisplayInfor(queryResult, rowMeta);
            ShowRepeatedComponentLockIfNeeded(queryResult);
            StopStatisticsPolling();
            MainScreenHost.Content = OperationScreen;
            DisplayProcess();
            return true;
        }

        private void ShowRepeatedComponentLockIfNeeded(QueryResult queryResult)
        {
            var trigger = FindRepeatedComponentLockTrigger(queryResult);
            if (trigger == null) return;

            var lockWindow = new RepeatedComponentLockWindow(
                trigger.BoardName,
                trigger.ComponentName,
                trigger.Block,
                trigger.Count,
                SoftwareSettingsManager.Current.RepeatedComponentUnlockPassword)
            {
                Owner = this
            };
            lockWindow.ShowDialog();
            RepeatedComponentRuntimeCounts[BuildRepeatedComponentKey(trigger.BoardName, trigger.ComponentName, trigger.Block)] = 0;
        }

        private RepeatedComponentLockTrigger FindRepeatedComponentLockTrigger(QueryResult queryResult)
        {
            int threshold = SoftwareSettingsManager.Current.RepeatedComponentLockCount;
            if (threshold <= 0) threshold = 8;
            if (queryResult?.DefectLocations == null || queryResult.DefectLocations.Count == 0) return null;

            string boardName = (queryResult.BoardName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(boardName)) return null;

            var candidates = queryResult.DefectLocations
                .Where(x => !string.IsNullOrWhiteSpace(x?.Name))
                .Select(x => new RepeatedComponentKey
                {
                    ComponentName = x.Name.Trim(),
                    Block = x.Block
                })
                .GroupBy(x => BuildRepeatedComponentKey(boardName, x.ComponentName, x.Block), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            if (candidates.Count == 0) return null;

            foreach (var candidate in candidates)
            {
                string key = BuildRepeatedComponentKey(boardName, candidate.ComponentName, candidate.Block);
                int confirmedCount = RepeatedComponentRuntimeCounts.TryGetValue(key, out var value) ? value : 0;
                int count = confirmedCount + 1;

                if (count >= threshold)
                {
                    return new RepeatedComponentLockTrigger
                    {
                        BoardName = boardName,
                        ComponentName = candidate.ComponentName,
                        Block = candidate.Block,
                        Count = count
                    };
                }
            }

            return null;
        }

        private void UpdateRepeatedComponentRuntimeCountsForCurrentInspection()
        {
            if (CurrentDisplayInfor == null) return;

            string boardName = (CurrentDisplayInfor.Model ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(boardName)) return;

            var currentKeys = (CurrentDisplayInfor.ComponentInfors ?? new List<ComponentInfor>())
                .Where(x => !string.IsNullOrWhiteSpace(x?.Name))
                .Select(x => BuildRepeatedComponentKey(boardName, x.Name, x.BlockID))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var boardPrefix = BuildRepeatedComponentBoardPrefix(boardName);
            var keysToReset = RepeatedComponentRuntimeCounts.Keys
                .Where(x => x.StartsWith(boardPrefix, StringComparison.OrdinalIgnoreCase) &&
                            !currentKeys.Contains(x, StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToReset)
            {
                RepeatedComponentRuntimeCounts[key] = 0;
            }

            foreach (var key in currentKeys)
            {
                RepeatedComponentRuntimeCounts[key] = RepeatedComponentRuntimeCounts.TryGetValue(key, out var count)
                    ? count + 1
                    : 1;
            }
        }

        private static string BuildRepeatedComponentKey(string boardName, string componentName, int block)
        {
            return $"{BuildRepeatedComponentBoardPrefix(boardName)}{(componentName ?? string.Empty).Trim()}|{block}";
        }

        private static string BuildRepeatedComponentBoardPrefix(string boardName)
        {
            return $"{(boardName ?? string.Empty).Trim()}|";
        }


        private void NavigateComponent(int step)
        {
            if (CurrentDisplayInfor == null) return;
            int count = CurrentDisplayInfor.ComponentInfors?.Count ?? 0;
            if (count <= 0) return;

            CurrentComponentLocation = (CurrentComponentLocation + step + count) % count;
            LoadComponentLocation(CurrentComponentLocation);
        }

        private void Window_Closed(object sender, EventArgs e) {
            StopStatisticsPolling();
            if (PanelImageView != null)
            {
                PanelImageView.BulkAllOkRequested -= PanelImageView_BulkAllOkRequested;
                PanelImageView.BulkAllNgRequested -= PanelImageView_BulkAllNgRequested;
                PanelImageView.Clicked -= PanelImageView_Clicked;
            }
            ResultPreviewImage.Clicked -= ResultPreviewImage_Clicked;
            SoftwareSettingsManager.Save();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadStatisticsData();
            if (!TryOpenFirstPendingInspection())
            {
                MainScreenHost.Content = StatisticsScreen;
                StartStatisticsPolling();
            }
        }

        private void menuSoftware_Click(object sender, RoutedEventArgs e) {
            var window = new SoftwareSettings();
            window.Closed += SoftwareSettingsWindow_Closed;
            window.Show();

        }

        private void SoftwareSettingsWindow_Closed(object sender, EventArgs e)
        {
            if (MainScreenHost.Content != OperationScreen) return;
            if (CurrentDisplayInfor == null || CurrentDisplayInfor.ComponentInfors.Count <= 0) return;
            if (CurrentComponentLocation < 0 || CurrentComponentLocation >= CurrentDisplayInfor.ComponentInfors.Count) return;

            UpdateAlarmTypeButtons(CurrentDisplayInfor.ComponentInfors[CurrentComponentLocation], keepCurrentPage: true);
        }

        private void menuOperationView_Click(object sender, RoutedEventArgs e)
        {
            StopStatisticsPolling();
            MainScreenHost.Content = OperationScreen;
            if (CurrentDisplayInfor != null && CurrentDisplayInfor.ComponentInfors.Count > 0)
            {
                CurrentComponentLocation = 0;
                LoadComponentLocation(CurrentComponentLocation);
            }
        }

        private void menuStatisticsView_Click(object sender, RoutedEventArgs e)
        {
            MainScreenHost.Content = StatisticsScreen;
            LoadStatisticsData();
            StartStatisticsPolling();
        }

        private void menuHelp_Click(object sender, RoutedEventArgs e) {
            UILib.ShowInformation(
                "Keyboard Shortcuts\n\n" +
                "Operation View\n" +
                "Left / A: Previous component\n" +
                "Right / D: Next component\n" +
                $"{GetConfirmOkShortcutDisplayText()} : Confirm OK\n" +
                "1..9 : Confirm alarm type button 1..9\n\n" +
                "Result View\n" +
                "Enter: Commit and open next inspection\n" +
                "Esc: Back to Operation to re-check\n" +
                "Click OK/NG: Commit and open next inspection");
        }

        private void menuAbout_Click(object sender, RoutedEventArgs e)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "N/A";
            UILib.ShowInformation(
                "AI-AOI Repair Station\n" +
                $"Version: {version}\n\n" +
                "Used for AOI repair confirmation workflow.");
        }

        private BitmapImage CreateImageSourceFromBytes(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0) return null;

            try
            {
                var image = new BitmapImage();
                using (var ms = new MemoryStream(imageBytes))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                }
                return image;
            }
            catch
            {
                return null;
            }
        }

        private System.Windows.Media.ImageSource ScaleImageSourceIfSmallerThanSize(
            System.Windows.Media.ImageSource source,
            double scale,
            double size)
        {
            if (source == null) return null;

            if (source is BitmapSource bitmapSource)
            {
                try
                {
                    double targetScale = scale;
                    if (size > 0)
                    {
                        double scaledWidth = source.Width * targetScale;
                        double scaledHeight = source.Height * targetScale;
                        double maxScaledSide = Math.Max(scaledWidth, scaledHeight);
                        if (maxScaledSide > size)
                        {
                            double maxSourceSide = Math.Max(source.Width, source.Height);
                            targetScale = maxSourceSide <= 0 ? targetScale : size / maxSourceSide;
                        }
                    }

                    if (Math.Abs(targetScale - 1.0) < 0.0001) return source;

                    var transformed = new TransformedBitmap(bitmapSource, new System.Windows.Media.ScaleTransform(targetScale, targetScale));
                    transformed.Freeze();
                    return transformed;
                }
                catch
                {
                    return source;
                }
            }

            return source;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (MainScreenHost.Content == ResultScreen)
            {
                if (e.Key == Key.Escape)
                {
                    if (CurrentDisplayInfor != null && CurrentDisplayInfor.ComponentInfors.Count > 0)
                    {
                        MainScreenHost.Content = OperationScreen;
                        IsConfirmingIssue = true;
                        CurrentComponentLocation = Math.Max(0, Math.Min(CurrentComponentLocation, CurrentDisplayInfor.ComponentInfors.Count - 1));
                        LoadComponentLocation(CurrentComponentLocation);
                        RefreshConfirmProgress();
                    }
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Enter)
                {
                    CommitCurrentInspectionAndMoveNext();
                    e.Handled = true;
                    return;
                }

                return;
            }

            if (MainScreenHost.Content != OperationScreen) return;
            if (CurrentDisplayInfor == null || CurrentDisplayInfor.ComponentInfors.Count <= 0) return;

            if (IsConfirmOkShortcutKey(e.Key))
            {
                if (IsConfirmingIssue)
                {
                    ConfirmCurrentIssue(false, "OK", "OK");
                    e.Handled = true;
                }
                return;
            }

            string shortcut = ResolveShortcutFromKey(e.Key);
            if (!string.IsNullOrWhiteSpace(shortcut))
            {
                if (shortcut == ".")
                {
                    if (TryNextAlarmTypePageByShortcut())
                    {
                        e.Handled = true;
                    }
                    return;
                }

                if (TryConfirmByShortcut(shortcut))
                {
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Left || e.Key == Key.A)
            {
                NavigateComponent(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.Right || e.Key == Key.D)
            {
                NavigateComponent(1);
                e.Handled = true;
            }
        }

        private string GetConfirmOkShortcut()
        {
            var shortcut = SoftwareSettingsManager.Current?.ConfirmOkShortcut;
            return SoftwareSettingsManager.IsValidConfirmOkShortcut(shortcut) ? shortcut.Trim() : "+";
        }

        private string GetConfirmOkShortcutDisplayText()
        {
            return GetConfirmOkShortcut();
        }

        private bool IsConfirmOkShortcutKey(Key key)
        {
            switch (GetConfirmOkShortcut())
            {
                case "+":
                    return key == Key.Add ||
                           (key == Key.OemPlus && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift);
                case "Space":
                    return key == Key.Space;
                case "Left Shift":
                    return key == Key.LeftShift;
                case "Right Shift":
                    return key == Key.RightShift;
                case "Down":
                    return key == Key.Down;
                default:
                    return false;
            }
        }

        private void CommitCurrentInspectionAndMoveNext()
        {
            if (CurrentDisplayInfor == null) return;
            if (CurrentConfirmResults.Any(x => !x.HasValue))
            {
                UILib.ShowWarning("Please confirm all components before commit.");
                return;
            }

            var defectByComponent = BuildDefectMapForCurrentInspection();
            if (!TrySaveCurrentInspectionImageLogs(out var imageLogError))
            {
                UILib.ShowError($"Save inspection image logs failed: {imageLogError}");
                return;
            }

            if (!SQL.CommitAndMoveInspection(CurrentDisplayInfor.InspectionID, defectByComponent, out var error))
            {
                UILib.ShowError($"Commit/move inspection failed: {error}");
                return;
            }

            UpdateRepeatedComponentRuntimeCountsForCurrentInspection();
            AccumulateSessionStatisticsForCurrentInspection();
            LoadStatisticsData();
            if (TryOpenFirstPendingInspection()) return;
            MainScreenHost.Content = StatisticsScreen;
            ResetConfirmIssueUI();
            StartStatisticsPolling();
        }

        private void AccumulateSessionStatisticsForCurrentInspection()
        {
            if (CurrentDisplayInfor == null) return;
            if (SessionCountedInspectionIds.Contains(CurrentDisplayInfor.InspectionID)) return;

            int inspectedPcbs = CurrentDisplayInfor.BlockCount;
            int inspectedComponents = CurrentDisplayInfor.TotalComponentCount;
            int ngAoiComponents = CurrentDisplayInfor.NgAoiComponentCount;

            if (inspectedPcbs <= 0)
            {
                inspectedPcbs = (CurrentDisplayInfor.ComponentInfors ?? new List<ComponentInfor>())
                    .Select(x => x.BlockID)
                    .Distinct()
                    .Count();
            }
            if (inspectedComponents <= 0)
            {
                inspectedComponents = CurrentDisplayInfor.ComponentInfors?.Count ?? 0;
            }
            if (ngAoiComponents <= 0)
            {
                ngAoiComponents = CurrentDisplayInfor.ComponentInfors?.Count ?? 0;
            }

            int ngComponents = 0;
            var aoiNgBlockIds = new HashSet<int>();
            var ngBlockIds = new HashSet<int>();

            int count = Math.Min(CurrentDisplayInfor.ComponentInfors.Count, CurrentConfirmDefectTypes.Count);
            for (int i = 0; i < count; i++)
            {
                // AOI-NG block: block has at least one component that needs operator confirmation.
                aoiNgBlockIds.Add(CurrentDisplayInfor.ComponentInfors[i].BlockID);

                string defect = (CurrentConfirmDefectTypes[i] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(defect) || string.Equals(defect, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ngComponents++;
                ngBlockIds.Add(CurrentDisplayInfor.ComponentInfors[i].BlockID);
            }

            SessionStats.InspectedPcbs += Math.Max(0, inspectedPcbs);
            SessionStats.NgPcbs += ngBlockIds.Count;
            SessionStats.InspectedComponents += Math.Max(0, inspectedComponents);
            SessionStats.NgAoiComponents += Math.Max(0, ngAoiComponents);
            SessionStats.NgComponents += Math.Max(0, ngComponents);

            // IPY definition (by block):
            // AOI direct pass blocks = total inspected blocks - AOI-NG blocks.
            int aoiDirectPassBlocks = inspectedPcbs - aoiNgBlockIds.Count;
            if (aoiDirectPassBlocks < 0) aoiDirectPassBlocks = 0;
            SessionStats.AoiDirectPassPcbs += aoiDirectPassBlocks;

            SessionCountedInspectionIds.Add(CurrentDisplayInfor.InspectionID);

            UpdateStatisticsSummary();
        }

        private void StatisticsPollTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (StatisticsPollTimer != null)
            {
                StatisticsPollTimer.Enabled = false;
            }

            if (Dispatcher == null || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (!IsLoaded) return;
                    PollStatisticsAndAutoOpen();
                }
                finally
                {
                    // Resume polling only when staying on Statistics screen.
                    if (StatisticsPollTimer != null && MainScreenHost.Content == StatisticsScreen)
                    {
                        StatisticsPollTimer.Enabled = true;
                    }
                }
            }));
        }

        private void PollStatisticsAndAutoOpen()
        {
            if (MainScreenHost.Content != StatisticsScreen) return;
            if (IsConfirmingIssue) return;

            bool wasEmptyBeforePoll = WasStatisticsEmpty;
            IsStatisticsSelectionHandlingEnabled = false;
            try
            {
                LoadStatisticsData();
            }
            finally
            {
                IsStatisticsSelectionHandlingEnabled = true;
            }

            if (!wasEmptyBeforePoll) return;
            TryOpenFirstPendingInspection();
        }

        private bool TryOpenFirstPendingInspection()
        {
            IsStatisticsSelectionHandlingEnabled = false;
            try
            {
                return TryOpenFirstPendingInspectionCore(showMarkPopup: true);
            }
            finally
            {
                IsStatisticsSelectionHandlingEnabled = true;
            }
        }

        private bool TryOpenFirstPendingInspectionCore(bool showMarkPopup)
        {
            int guard = 0;
            while (guard++ < 500)
            {
                var rows = (dgStatistics.ItemsSource as IEnumerable<InspectionStatisticRow>)?.ToList() ?? new List<InspectionStatisticRow>();
                if (rows.Count == 0)
                {
                    WasStatisticsEmpty = true;
                    return false;
                }

                var row = rows[0];
                WasStatisticsEmpty = false;
                dgStatistics.SelectedItem = row;

                if (row.MarkCount > 0)
                {
                    MainScreenHost.Content = StatisticsScreen;

                    if (showMarkPopup)
                    {
                        MessageBox.Show(
                            "Mark Error: Inspection has data in Mark table.\nThis inspection will be moved to Repair and skipped.",
                            "Mark Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }

                    if (!SQL.MoveInspectionToRepair(row.InspectionID, out var moveError))
                    {
                        UILib.ShowError($"Move inspection failed: {moveError}");
                        return false;
                    }

                    LoadStatisticsData();
                    continue;
                }

                return OpenInspection(row.InspectionID);
            }

            UILib.ShowError("Could not resolve next inspection due to unexpected loop state.");
            return false;
        }

        private void ProcessSelectedStatisticsInspection(InspectionStatisticRow row)
        {
            if (row == null) return;

            if (row.MarkCount > 0)
            {
                MainScreenHost.Content = StatisticsScreen;
                MessageBox.Show(
                    "Mark Error: Inspection has data in Mark table.\nThis inspection will be moved to Repair and skipped.",
                    "Mark Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                if (!SQL.MoveInspectionToRepair(row.InspectionID, out var moveError))
                {
                    UILib.ShowError($"Move inspection failed: {moveError}");
                    return;
                }

                LoadStatisticsData();
                TryOpenFirstPendingInspection();
                return;
            }

            OpenInspection(row.InspectionID);
        }

        private void StartStatisticsPolling()
        {
            if (StatisticsPollTimer == null) return;
            StatisticsPollTimer.Enabled = true;
        }

        private void StopStatisticsPolling()
        {
            if (StatisticsPollTimer == null) return;
            StatisticsPollTimer.Enabled = false;
        }

        private Dictionary<Guid, string> BuildDefectMapForCurrentInspection()
        {
            var map = new Dictionary<Guid, string>();
            if (CurrentDisplayInfor?.ComponentInfors == null) return map;

            int count = Math.Min(CurrentDisplayInfor.ComponentInfors.Count, CurrentConfirmDefectTypes.Count);
            for (int i = 0; i < count; i++)
            {
                var component = CurrentDisplayInfor.ComponentInfors[i];
                if (component == null || component.ComponentID == Guid.Empty) continue;

                string defectType = CurrentConfirmDefectTypes[i];
                if (string.IsNullOrWhiteSpace(defectType))
                {
                    defectType = CurrentConfirmResults.ElementAtOrDefault(i) == true ? "NG" : "OK";
                }

                map[component.ComponentID] = defectType.Trim();
            }

            return map;
        }

        private bool TrySaveCurrentInspectionImageLogs(out string error)
        {
            error = null;
            try
            {
                SaveCurrentInspectionTextLogs();
                SaveCurrentInspectionImageLogs();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Logger.Error(ex, "Save inspection image logs failed.");
                return false;
            }
        }

        private void SaveCurrentInspectionImageLogs()
        {
            if (CurrentDisplayInfor?.ComponentInfors == null) return;

            string historyRoot = SoftwareSettingsManager.Current.HistoryDataRootPath;
            string imageRoot = SoftwareSettingsManager.Current.ImageDataRootPath;
            if (string.IsNullOrWhiteSpace(historyRoot) && string.IsNullOrWhiteSpace(imageRoot)) return;

            DateTime inspectTime = CurrentDisplayInfor.InspectTime;
            string compactDate = inspectTime.ToString("yyyyMMdd");
            string imageDate = inspectTime.ToString("yyyy_MM_dd");
            string timestamp = inspectTime.ToString("yyyyMMddHHmmss");
            string boardName = SanitizePathPart(CurrentDisplayInfor.Model, "Board");
            string panelSn = SanitizePathPart(CurrentDisplayInfor.SN, CurrentDisplayInfor.InspectionID.ToString());
            string line = SanitizePathPart(CurrentDisplayInfor.Line, "Line");

            int count = CurrentDisplayInfor.ComponentInfors.Count;
            for (int i = 0; i < count; i++)
            {
                var component = CurrentDisplayInfor.ComponentInfors[i];
                if (component?.AlarmInfors == null || component.AlarmInfors.Count == 0) continue;

                string componentName = SanitizePathPart(component.Name, "Component");
                string block = SanitizePathPart(component.BlockID.ToString(), "0");
                string confirmType = GetConfirmedDefectType(i);
                string resultFolder = IsConfirmedOk(i, confirmType) ? "RPASS" : "FAIL";

                foreach (var alarm in component.AlarmInfors)
                {
                    if (alarm == null || alarm.AlarmID == Guid.Empty) continue;

                    string alarmId = SanitizePathPart(alarm.AlarmID.ToString(), "Alarm");
                    string alarmType = SanitizePathPart(alarm.AlarmType, confirmType);

                    if (!string.IsNullOrWhiteSpace(historyRoot))
                    {
                        string historyFolder = Path.Combine(
                            historyRoot,
                            boardName,
                            compactDate,
                            $"{panelSn}_{timestamp}",
                            "Alarm");
                        SaveImageBytes(alarm.TopImageBytes, Path.Combine(historyFolder, $"{componentName}_{block}_{alarmId}_top.jpg"));
                        SaveImageBytes(alarm.SideImageBytes, Path.Combine(historyFolder, $"{componentName}_{block}_{alarmId}_side.jpg"));
                    }

                    if (!string.IsNullOrWhiteSpace(imageRoot))
                    {
                        string imageFolder = Path.Combine(imageRoot, resultFolder, boardName, imageDate);
                        string imageBaseName = $"{line}_{panelSn}_{timestamp}_{componentName}@{block}_{alarmType}";
                        SaveImageBytes(alarm.TopImageBytes, Path.Combine(imageFolder, $"{imageBaseName}_top.jpg"));
                        SaveImageBytes(alarm.SideImageBytes, Path.Combine(imageFolder, $"{imageBaseName}_side.jpg"));
                    }
                }
            }
        }

        private void SaveCurrentInspectionTextLogs()
        {
            if (CurrentDisplayInfor == null) return;

            SaveOffsetTextLog(
                SoftwareSettingsManager.Current.OffsetNgRootPath,
                GetConfirmedComponents(isOk: false),
                useConfirmedAlarmType: true);

            SaveOffsetTextLog(
                SoftwareSettingsManager.Current.OffsetOkRootPath,
                GetConfirmedComponents(isOk: true),
                useConfirmedAlarmType: false);

            SaveShopfloorExportLogs();
        }

        private void SaveOffsetTextLog(string rootPath, IEnumerable<Tuple<int, ComponentInfor>> components, bool useConfirmedAlarmType)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) return;

            var rows = components?.Where(x => x?.Item2 != null).ToList() ?? new List<Tuple<int, ComponentInfor>>();
            if (rows.Count == 0) return;

            DateTime inspectTime = CurrentDisplayInfor.InspectTime;
            string timestamp = inspectTime.ToString("yyyyMMddHHmmss");
            string boardName = CurrentDisplayInfor.Model ?? string.Empty;
            string line = CurrentDisplayInfor.Line ?? string.Empty;
            string panelSn = CurrentDisplayInfor.SN ?? CurrentDisplayInfor.InspectionID.ToString();
            string rail = CurrentDisplayInfor.RailID.ToString();

            var lines = new List<string>
            {
                boardName,
                line,
                "LOCATION_SEQ\tX\tY\tTheta\tResult\tType\tBarcode\tTime\tRail"
            };

            foreach (var row in rows)
            {
                int index = row.Item1;
                var component = row.Item2;
                string alarmType = useConfirmedAlarmType
                    ? GetConfirmedDefectType(index)
                    : GetComponentAlarmType(component);

                lines.Add(string.Join("\t", new[]
                {
                    $"{component.Name}_{component.BlockID}",
                    FormatLogNumber(component.ComponentX),
                    FormatLogNumber(component.ComponentY),
                    "0",
                    alarmType,
                    component.Catalog ?? string.Empty,
                    panelSn,
                    timestamp,
                    rail
                }));
            }

            string folder = Path.Combine(rootPath, SanitizePathPart(boardName, "Board"));
            string filePath = Path.Combine(folder, $"{SanitizePathPart(panelSn, CurrentDisplayInfor.InspectionID.ToString())}_{timestamp}.ttt");
            WriteAllLinesCreatingDirectory(filePath, lines);
        }

        private void SaveShopfloorExportLogs()
        {
            string rootPath = SoftwareSettingsManager.Current.ShopfloorExportRootPath;
            if (string.IsNullOrWhiteSpace(rootPath)) return;

            var components = CurrentDisplayInfor.ComponentInfors
                .Select((component, index) => Tuple.Create(index, component))
                .Where(x => x.Item2 != null)
                .ToList();

            DateTime inspectTime = CurrentDisplayInfor.InspectTime;
            string compactDate = inspectTime.ToString("yyyyMMdd");
            string timestamp = inspectTime.ToString("yyyyMMddHHmmss");
            string boardName = CurrentDisplayInfor.Model ?? string.Empty;
            string line = CurrentDisplayInfor.Line ?? string.Empty;
            string panelSn = CurrentDisplayInfor.SN ?? CurrentDisplayInfor.InspectionID.ToString();
            int totalComponentCount = CurrentDisplayInfor.TotalComponentCount > 0
                ? CurrentDisplayInfor.TotalComponentCount
                : CurrentDisplayInfor.ComponentInfors.Count;

            var componentsByBlock = components
                .GroupBy(x => x.Item2.BlockID)
                .ToDictionary(g => g.Key, g => g.ToList());
            var blockNumbers = GetShopfloorBlockNumbers(componentsByBlock.Keys);

            foreach (var blockNumber in blockNumbers)
            {
                var blockRows = componentsByBlock.TryGetValue(blockNumber, out var rows)
                    ? rows
                    : new List<Tuple<int, ComponentInfor>>();
                var failedRows = blockRows
                    .Where(x => !IsConfirmedOk(x.Item1, GetConfirmedDefectType(x.Item1)))
                    .ToList();

                var lines = new List<string>
                {
                    $"{boardName};{line};{panelSn};{timestamp};{totalComponentCount};"
                };

                if (failedRows.Count == 0)
                {
                    lines.Add("PASS;");
                }
                else
                {
                    lines.Add($"FAIL;{failedRows.Count};");
                    lines.Add(string.Concat(failedRows.Select(x =>
                    {
                        var component = x.Item2;
                        return $"{component.Name}_{component.BlockID},{component.Catalog ?? string.Empty},{GetConfirmedDefectType(x.Item1)};";
                    })));
                }

                string folder = Path.Combine(
                    rootPath,
                    SanitizePathPart(boardName, "Board"),
                    compactDate);
                string fileName = $"{SanitizePathPart(line, "Line")}_{timestamp}_{SanitizePathPart(blockNumber.ToString(), "0")}.txt";
                WriteAllLinesCreatingDirectory(Path.Combine(folder, fileName), lines);
            }
        }

        private List<int> GetShopfloorBlockNumbers(IEnumerable<int> componentBlockNumbers)
        {
            var blocks = (CurrentDisplayInfor?.BlockNumbers ?? new List<int>())
                .Concat(componentBlockNumbers ?? Enumerable.Empty<int>())
                .Where(x => x != 0)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (blocks.Count == 0 && CurrentDisplayInfor?.BlockCount > 0)
            {
                blocks = Enumerable.Range(1, CurrentDisplayInfor.BlockCount).ToList();
            }

            return blocks;
        }

        private IEnumerable<Tuple<int, ComponentInfor>> GetConfirmedComponents(bool isOk)
        {
            return (CurrentDisplayInfor?.ComponentInfors ?? new List<ComponentInfor>())
                .Select((component, index) => Tuple.Create(index, component))
                .Where(x => x.Item2 != null && IsConfirmedOk(x.Item1, GetConfirmedDefectType(x.Item1)) == isOk);
        }

        private static void WriteAllLinesCreatingDirectory(string filePath, IEnumerable<string> lines)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            string folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllLines(filePath, lines ?? Enumerable.Empty<string>(), Encoding.UTF8);
        }

        private static string FormatLogNumber(double value)
        {
            return value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string GetComponentAlarmType(ComponentInfor component)
        {
            if (component?.AlarmTypes == null || component.AlarmTypes.Count == 0) return string.Empty;

            return string.Join(",", component.AlarmTypes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private string GetConfirmedDefectType(int componentIndex)
        {
            string defectType = CurrentConfirmDefectTypes.ElementAtOrDefault(componentIndex);
            if (!string.IsNullOrWhiteSpace(defectType))
            {
                return defectType.Trim();
            }

            return CurrentConfirmResults.ElementAtOrDefault(componentIndex) == true ? "NG" : "OK";
        }

        private bool IsConfirmedOk(int componentIndex, string defectType)
        {
            if (!string.IsNullOrWhiteSpace(defectType))
            {
                return string.Equals(defectType.Trim(), "OK", StringComparison.OrdinalIgnoreCase);
            }

            return CurrentConfirmResults.ElementAtOrDefault(componentIndex) != true;
        }

        private static void SaveImageBytes(byte[] imageBytes, string filePath)
        {
            if (imageBytes == null || imageBytes.Length == 0 || string.IsNullOrWhiteSpace(filePath)) return;

            string folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllBytes(filePath, imageBytes);
        }

        private static string SanitizePathPart(string value, string fallback)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(c, '_');
            }

            return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
        }
    }

    public class ConfirmChoice
    {
        public bool HasIssue { get; set; }
        public string DefectType { get; set; }
        public string DisplayType { get; set; }
        public string Shortcut { get; set; }
    }

    public class RepeatedComponentKey
    {
        public string ComponentName { get; set; }
        public int Block { get; set; }
    }

    public class RepeatedComponentLockTrigger
    {
        public string BoardName { get; set; }
        public string ComponentName { get; set; }
        public int Block { get; set; }
        public int Count { get; set; }
    }

    public class AlarmTypeOption
    {
        public string Original { get; set; }
        public string Display { get; set; }
    }

    public class ComponentInfor
    {
        public Guid ComponentID { get; set; }
        public string Name { get; set; }
        public string Catalog { get; set; }
        public int BlockID { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double ComponentX { get; set; }
        public double ComponentY { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Angle { get; set; }
        public byte[] TopImageBytes { get; set; }
        public byte[] SideImageBytes { get; set; }
        public byte[] TopReferenceImageBytes { get; set; }
        public byte[] SideReferenceImageBytes { get; set; }
        public byte[] AlarmTopImageBytes { get; set; }
        public byte[] AlarmSideImageBytes { get; set; }
        public List<AlarmImageInfo> AlarmInfors { get; set; } = new List<AlarmImageInfo>();
        public List<string> AlarmTypes { get; set; } = new List<string>();
    }

    public class DisplayInfor
    {
        public Guid InspectionID { get; set; }
        public string Model { get; set; }
        public string SN { get; set; }
        public bool Status { get; set; }
        public bool HasMark { get; set; }
        public DateTime InspectTime { get; set; }
        public int ConveyorIndex { get; set; }
        public int RailID { get; set; }
        public string Station { get; set; }
        public string ProductLot { get; set; }
        public string Line { get; set; }
        public int BlockCount { get; set; }
        public List<int> BlockNumbers { get; set; } = new List<int>();
        public int TotalComponentCount { get; set; }
        public int NgAoiComponentCount { get; set; }
        public Mat PanelImage { get; set; }
        public List<ComponentInfor> ComponentInfors = new List<ComponentInfor>();
        public ObservableCollection<MyDrawedRectangle> MyDrawedRectangle = new ObservableCollection<MyDrawedRectangle>();
        public ObservableCollection<MyDrawedText> MyDrawedText = new ObservableCollection<MyDrawedText>();
    }

    public class SessionStatistics
    {
        public int InspectedPcbs { get; set; }
        public int NgPcbs { get; set; }
        public int InspectedComponents { get; set; }
        public int NgAoiComponents { get; set; }
        public int NgComponents { get; set; }
        public int AoiDirectPassPcbs { get; set; }
    }
}
