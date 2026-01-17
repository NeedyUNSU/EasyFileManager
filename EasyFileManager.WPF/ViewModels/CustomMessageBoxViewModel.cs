using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using MaterialDesignThemes.Wpf;
using EasyFileManager.WPF.Models;

namespace EasyFileManager.WPF.ViewModels
{
    public partial class CustomMessageBoxViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _message;

        [ObservableProperty]
        private PackIconKind _iconKind;

        [ObservableProperty]
        private string _iconColor;

        [ObservableProperty]
        private MessageBoxResult _result;

        [ObservableProperty]
        private bool _showIcon;

        public ObservableCollection<DialogButton> Buttons { get; set; }

        // PUBLICZNY konstruktor bez parametrów dla XAML
        public CustomMessageBoxViewModel()
        {
            Title = "Message";
            Message = string.Empty;
            Result = MessageBoxResult.None;
            Buttons = new ObservableCollection<DialogButton>();
            ShowIcon = false;
            IconKind = PackIconKind.Information; // Domyślna wartość
            IconColor = "#757575";
        }

        // Konstruktor z parametrem dla code-behind
        public CustomMessageBoxViewModel(CustomMessageBoxModel model) : this()
        {
            Title = model.Title;
            Message = model.Message;

            SetupIcon(model.Image);
            SetupButtons(model);
        }

        private void SetupIcon(MessageBoxImage image)
        {
            switch (image)
            {
                case MessageBoxImage.Information:
                    IconKind = PackIconKind.Information;
                    IconColor = "#2196F3";
                    ShowIcon = true;
                    break;
                case MessageBoxImage.Question:
                    IconKind = PackIconKind.HelpCircle;
                    IconColor = "#FF9800";
                    ShowIcon = true;
                    break;
                case MessageBoxImage.Warning:
                    IconKind = PackIconKind.Alert;
                    IconColor = "#FF9800";
                    ShowIcon = true;
                    break;
                case MessageBoxImage.Error:
                    IconKind = PackIconKind.CloseCircle;
                    IconColor = "#F44336";
                    ShowIcon = true;
                    break;
                case MessageBoxImage.None:
                default:
                    IconKind = PackIconKind.Information; // Domyślna wartość (nie będzie widoczna)
                    IconColor = "#757575";
                    ShowIcon = false;
                    break;
            }
        }

        private void SetupButtons(CustomMessageBoxModel model)
        {
            Buttons.Clear();

            if (model.CustomButtons != null && model.CustomButtons.Count > 0)
            {
                foreach (var button in model.CustomButtons)
                {
                    Buttons.Add(button);
                }
                return;
            }

            switch (model.Buttons)
            {
                case MessageBoxButton.OK:
                    Buttons.Add(new DialogButton("OK", MessageBoxResult.OK, true, false));
                    break;

                case MessageBoxButton.OKCancel:
                    Buttons.Add(new DialogButton("CANCEL", MessageBoxResult.Cancel, false, true));
                    Buttons.Add(new DialogButton("OK", MessageBoxResult.OK, true, false));
                    break;

                case MessageBoxButton.YesNo:
                    Buttons.Add(new DialogButton("NO", MessageBoxResult.No, false, true));
                    Buttons.Add(new DialogButton("YES", MessageBoxResult.Yes, true, false));
                    break;

                case MessageBoxButton.YesNoCancel:
                    Buttons.Add(new DialogButton("CANCEL", MessageBoxResult.Cancel, false, true));
                    Buttons.Add(new DialogButton("NO", MessageBoxResult.No, false, false));
                    Buttons.Add(new DialogButton("YES", MessageBoxResult.Yes, true, false));
                    break;
            }
        }

        [RelayCommand]
        private void ButtonClick(MessageBoxResult result)
        {
            Result = result;
        }

        [RelayCommand]
        private void CloseDialog()
        {
            Result = MessageBoxResult.Cancel;
        }
    }
}