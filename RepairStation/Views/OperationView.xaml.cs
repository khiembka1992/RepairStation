using System.Windows.Controls;
using System.Windows.Shapes;
using Andrew.Controls;

namespace AI_AOI.Views
{
    public partial class OperationView : UserControl
    {
        public OperationView()
        {
            InitializeComponent();
        }

        public TextBlock AlarmedStatsText => tbAlarmedStats;
        public TextBlock HeaderBoardInfoText => tbHeaderBoardInfo;
        public ImageBox PanelImage => PanelImageView;
        public Image ComponentImage => ComponentImageView;
        public Image ComponentReferenceImage => ComponentReferenceImageView;
        public Image AlarmComponentImage => AlarmComponentImageView;
        public Grid AlarmButtonsGrid => gButtons;
    }
}
