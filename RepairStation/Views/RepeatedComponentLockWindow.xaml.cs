using System.ComponentModel;
using System.Windows;

namespace AI_AOI.Views
{
    public partial class RepeatedComponentLockWindow : Window
    {
        private readonly string ExpectedPassword;
        private bool IsUnlocked;

        public RepeatedComponentLockWindow(
            string boardName,
            string componentName,
            int block,
            int count,
            string expectedPassword)
        {
            InitializeComponent();
            ExpectedPassword = expectedPassword ?? string.Empty;
            DataContext = new
            {
                LockMessage =
                    $"Board Name: {boardName}\n" +
                    $"Component Name: {componentName}\n" +
                    $"Block: {block}\n" +
                    $"Continuous Count: {count}"
            };
            Loaded += RepeatedComponentLockWindow_Loaded;
        }

        private void RepeatedComponentLockWindow_Loaded(object sender, RoutedEventArgs e)
        {
            pbPassword.Focus();
        }

        private void UnlockButton_Click(object sender, RoutedEventArgs e)
        {
            if (pbPassword.Password == ExpectedPassword)
            {
                IsUnlocked = true;
                DialogResult = true;
                Close();
                return;
            }

            MessageBox.Show(
                "Password is incorrect.",
                "Unlock Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            pbPassword.Clear();
            pbPassword.Focus();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!IsUnlocked)
            {
                e.Cancel = true;
            }
        }
    }
}
