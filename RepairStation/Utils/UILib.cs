using System;
using System.Windows;

namespace AIOT.Utils
{
    class UILib
    {
        public static void ShowError(string msg)
        {
            MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        public static void ShowWarning(string msg)
        {
            MessageBox.Show(msg, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static void ShowInformation(string msg)
        {
            MessageBox.Show(msg, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static bool  ShowConfirm(string msg)
        {
              var result = MessageBox.Show(msg, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);

            return result == MessageBoxResult.Yes;
        }
    }
}
