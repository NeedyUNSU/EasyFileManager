using System.Windows.Controls;
using EasyFileManager.WPF.ViewModels;

namespace EasyFileManager.WPF.Views;

/// <summary>
/// Interaction logic for BackupPanel.xaml
/// </summary>
public partial class BackupPanel : UserControl
{
    public BackupPanel()
    {
        InitializeComponent();
    }

    private async void EnabledCheckBox_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is BackupJobViewModel jobVm)
        {
            if (DataContext is BackupViewModel vm)
            {
                // Set the new value based on checkbox state
                var newValue = checkBox.IsChecked == true;

                // Update the job
                jobVm.IsEnabled = newValue;

                // Trigger the toggle command
                vm.SelectedJob = jobVm;
                if (vm.ToggleJobEnabledCommand.CanExecute(null))
                {
                    await vm.ToggleJobEnabledCommand.ExecuteAsync(null);
                }
            }
        }
    }
}
