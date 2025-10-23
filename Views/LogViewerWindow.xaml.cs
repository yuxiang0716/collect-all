using System.Windows;
using collect_all.Services;

namespace collect_all.Views
{
    public partial class LogViewerWindow : Window
    {
        public LogViewerWindow()
        {
            InitializeComponent();
            LogListBox.ItemsSource = LogService.Logs;
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            LogService.Clear();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
