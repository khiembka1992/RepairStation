using System;
using System.Windows;
using System.Windows.Controls;

namespace AI_AOI.Views
{
    public partial class DeleteInspectionWindow : Window
    {
        private readonly string _expectedPassword;

        public DeleteInspectionWindow(
            string barcode,
            Guid inspectionId,
            string expectedPassword)
        {
            InitializeComponent();
            _expectedPassword = expectedPassword ?? string.Empty;
            DataContext = new
            {
                InspectionMessage =
                    $"Barcode: {barcode}\n" +
                    $"Inspection ID: {inspectionId}"
            };
            Loaded += DeleteInspectionWindow_Loaded;
        }

        private void DeleteInspectionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            pbPassword.Focus();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            tbError.Visibility = Visibility.Collapsed;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.Equals(pbPassword.Password, _expectedPassword, StringComparison.Ordinal))
            {
                DialogResult = true;
                Close();
                return;
            }

            tbError.Visibility = Visibility.Visible;
            pbPassword.Clear();
            pbPassword.Focus();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
