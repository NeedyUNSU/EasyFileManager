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

namespace EasyFileManager.WPF.Views
{
    /// <summary>
    /// Interaction logic for RenameDialog.xaml
    /// </summary>
    public partial class RenameDialog : Window
    {
        public string NewName { get; private set; } = string.Empty;

        public RenameDialog(string currentName)
        {
            InitializeComponent();
            NewNameTextBox.Text = currentName;
            NewNameTextBox.Focus();
            NewNameTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            NewName = NewNameTextBox.Text;
            DialogResult = true;
            Close();
        }
    }
}
