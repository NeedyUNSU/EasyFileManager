using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EasyFileManager.WPF.Views;

/// <summary>
/// Interaction logic for DeleteConfirmDialog.xaml
/// </summary>
public partial class DeleteConfirmDialog : Window
{
    public bool Confirmed { get; private set; }

    public DeleteConfirmDialog(string itemName)
    {
        InitializeComponent();
        MessageTextBlock.Text = $"Are you sure you want to delete '{itemName}'?";
    }

    public DeleteConfirmDialog(IEnumerable<string> itemNames)
    {
        InitializeComponent();

        var items = itemNames.ToList();
        var count = items.Count;

        if (count == 1)
        {
            MessageTextBlock.Text = $"Are you sure you want to delete '{items[0]}'?";
        }
        else
        {
            MessageTextBlock.Text = $"Are you sure you want to delete {count} items?";
            ItemsListBorder.Visibility = Visibility.Visible;

            var displayItems = items.Take(20).ToList();
            ItemsList.ItemsSource = displayItems;

            if (count > 20)
            {
                displayItems.Add($"... and {count - 20} more");
            }
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }
}
