using System.Windows;

namespace AI_AOI.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            Loaded += LoginWindow_Loaded;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            tbUser.Focus();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string user = (tbUser.Text ?? string.Empty).Trim();
            string password = pbPassword.Password ?? string.Empty;

            if (user == "1" && password == "1")
            {
                DialogResult = true;
                Close();
                return;
            }

            MessageBox.Show(
                "User or password is incorrect. The program will close.",
                "Login Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            DialogResult = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
