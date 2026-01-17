using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace EasyFileManager.WPF.Models
{
    public class CustomMessageBoxModel
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public MessageBoxButton Buttons { get; set; }
        public MessageBoxImage Image { get; set; }
        public MessageBoxResult DefaultResult { get; set; }
        public List<DialogButton> CustomButtons { get; set; }

        public CustomMessageBoxModel()
        {
            Title = "Message";
            Message = string.Empty;
            Buttons = MessageBoxButton.OK;
            Image = MessageBoxImage.None;
            DefaultResult = MessageBoxResult.None;
            CustomButtons = new List<DialogButton>();
        }
    }

    public partial class DialogButton : ObservableObject
    {
        [ObservableProperty]
        private string _content;

        [ObservableProperty]
        private MessageBoxResult _result;

        [ObservableProperty]
        private bool _isDefault;

        [ObservableProperty]
        private bool _isCancel;

        public DialogButton(string content, MessageBoxResult result, bool isDefault = false, bool isCancel = false)
        {
            Content = content;
            Result = result;
            IsDefault = isDefault;
            IsCancel = isCancel;
        }
    }
}