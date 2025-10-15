// 檔案: App.xaml.cs (已修正命名衝突)
using System.IO;
using System.Windows;
using collect_all.Services;
using collect_all.ViewModels;
using collect_all.Views;

namespace collect_all
{
    // VVVV 在這裡明確指定 : System.Windows.Application VVVV
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (!string.IsNullOrEmpty(exeDir))
            {
                Directory.SetCurrentDirectory(exeDir);
            }
            
            // 維持不變
            DataSendService.InitializeDatabase();   
            var mainViewModel = new MainViewModel();
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };
            mainWindow.Show();
        }
    }
}