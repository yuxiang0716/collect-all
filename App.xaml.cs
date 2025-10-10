// 檔案: App.xaml.cs
using System; // 新增
using System.IO;  // 新增
using System.Windows;

namespace collect_all
{
    public partial class App : System.Windows.Application
    {
        // --- 新增這整個 OnStartup 方法 ---
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 這是確保開機啟動時工作目錄正確的關鍵程式碼
            // 它能防止找不到 .ico 檔案或其他資源檔的問題
            string exeDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (!string.IsNullOrEmpty(exeDir))
            {
                Directory.SetCurrentDirectory(exeDir);
            }
        }
    }
}