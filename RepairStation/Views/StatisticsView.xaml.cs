using System;
using System.Windows;
using System.Windows.Controls;

namespace AI_AOI.Views
{
    public partial class StatisticsView : UserControl
    {
        public event EventHandler BulkAllOkRequested;
        public event EventHandler BulkAllNgRequested;

        public StatisticsView()
        {
            InitializeComponent();
        }

        private void AllOkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            BulkAllOkRequested?.Invoke(this, EventArgs.Empty);
        }

        private void AllNgMenuItem_Click(object sender, RoutedEventArgs e)
        {
            BulkAllNgRequested?.Invoke(this, EventArgs.Empty);
        }

        public TextBox SearchBarcodeTextBox => tbSearchBarcode;
        public Button SearchButton => btnSearch;
        public DataGrid StatisticsGrid => dgStatistics;
        public TextBox FilterBoardTextBox => tbFilterBoard;
        public TextBox FilterLineTextBox => tbFilterLine;
        public TextBox FilterProductLotTextBox => tbFilterProductLot;
        public TextBox FilterStationTextBox => tbFilterStation;
        public Button ApplyFilterButton => btnApplyFilter;
        public TextBlock QtyInspectedPcbsText => tbQtyInspectedPCBs;
        public TextBlock QtyNgPcbsText => tbQtyOfNgPCBs;
        public TextBlock NgPcbRateText => tbNgPcbRate;
        public TextBlock QtyInspectedComponentsText => tbQtyInspectedComponents;
        public TextBlock QtyNgAoiComponentsText => tbQtyOfNgAoiComponents;
        public TextBlock QtyNgComponentsText => tbQtyOfNgComponents;
        public TextBlock NgComponentRateText => tbNgComponentRate;
        public TextBlock NgAoiComponentRateText => tbNgAoiComponentRate;
        public TextBlock IpyText => tbIpy;
        public TextBlock FpyText => tbFpy;
    }
}
