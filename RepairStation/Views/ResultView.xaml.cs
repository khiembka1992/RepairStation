using System.Windows.Controls;
using Andrew.Controls;

namespace AI_AOI.Views
{
    public partial class ResultView : UserControl
    {
        public ResultView()
        {
            InitializeComponent();
        }
        
        public TextBlock InformationText => tbInformation;
        public ImageBox PreviewImage => ibImage;
        public TextBlock RemainText => tbRemain;
        public TextBlock StatisticsText => tbStatistics;
        public Button StatusText => tbStatus;
    }
}
