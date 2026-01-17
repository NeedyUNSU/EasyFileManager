using System.Windows;
using EasyFileManager.WPF.Models;
using EasyFileManager.WPF.ViewModels;

namespace EasyFileManager.WPF.Views
{
    public partial class CustomMessageBox : Window
    {
        private CustomMessageBoxViewModel _viewModel;

        public MessageBoxResult Result => _viewModel.Result;

        public CustomMessageBox(CustomMessageBoxModel model)
        {
            InitializeComponent();
            _viewModel = new CustomMessageBoxViewModel(model);
            DataContext = _viewModel;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        // Static helper methods
        public static MessageBoxResult Show(string message)
        {
            return Show(message, "Message", MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string message, string title)
        {
            return Show(message, title, MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons)
        {
            return Show(message, title, buttons, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
        {
            var model = new CustomMessageBoxModel
            {
                Message = message,
                Title = title,
                Buttons = buttons,
                Image = icon
            };

            return ShowInternal(model);
        }

        public static MessageBoxResult ShowCustom(CustomMessageBoxModel model)
        {
            return ShowInternal(model);
        }

        private static MessageBoxResult ShowInternal(CustomMessageBoxModel model)
        {
            var messageBox = new CustomMessageBox(model);

            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded)
            {
                messageBox.Owner = Application.Current.MainWindow;
            }

            messageBox.ShowDialog();

            return messageBox.Result;
        }
    }
}