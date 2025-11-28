using System.Windows;

namespace EasyFileManager.WPF.Views;

/// <summary>
/// Dialog for entering password for encrypted archives
/// </summary>
public partial class ArchivePasswordDialog : Window
{
    public string? Password { get; private set; }
    public bool RememberPassword => RememberPasswordCheckBox.IsChecked == true;
    public bool Confirmed { get; private set; }

    public ArchivePasswordDialog(string archiveName)
    {
        InitializeComponent();
        ArchiveNameTextBlock.Text = archiveName;
        PasswordBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Password = PasswordBox.Password;
        
        if (string.IsNullOrWhiteSpace(Password))
        {
            MessageBox.Show(
                "Please enter a password.",
                "Password Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            PasswordBox.Focus();
            return;
        }

        Confirmed = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
