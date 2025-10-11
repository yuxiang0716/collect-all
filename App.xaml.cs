// 檔案: App.xaml.cs (最終正確版本)

using System;
using System.IO;
using System.Windows;

namespace collect_all
{
    public partial class App : System.Windows.Application
    {
        // --- App() 建構函式 ---
        // 檔案: App.xaml.cs

public App()
{
    try
    {
        SoftwareSend.Initialize();
    }
    catch (Exception ex)
    {
        // VVVV 就是修改下面這一行 VVVV
        // 在 MessageBox 前面加上 System.Windows. 來明確指定使用 WPF 的版本
        System.Windows.MessageBox.Show($"應用程式啟動時發生嚴重錯誤，無法初始化資料庫：\n\n{ex.ToString()}", "啟動錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        
        Shutdown();
    }
}

        // --- 您原本的 OnStartup 方法維持不變 ---
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (!string.IsNullOrEmpty(exeDir))
            {
                Directory.SetCurrentDirectory(exeDir);
            }
        }
    }
}