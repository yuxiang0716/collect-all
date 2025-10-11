// 檔案: StartupManager.cs (已修正所有警告的版本)
using Microsoft.Win32;
using System;
using System.IO; // <--- 建議加上 using System.IO;

public static class StartupManager
{
    // 這裡的名稱會顯示在工作管理員的「啟動」分頁中
    private const string AppName = "CollectAll System Info";

    private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// 檢查目前是否已設定為開機啟動
    /// </summary>
    public static bool IsStartupSet()
    {
        try
        {
            // 使用 var，編譯器會自動推斷 key 的類型為 RegistryKey? (可為 null)
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                if (key == null) return false;

                // 使用 is 模式匹配，可以安全地檢查 value 是否存在、是否為 string，並賦值給新變數 path
                if (key.GetValue(AppName) is string pathValue)
                {
                    // 先取得目前的執行檔路徑，並檢查是否為 null
                    var currentPath = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(currentPath)) return false;

                    // 移除登錄檔中路徑的引號以進行比較
                    string registryPath = pathValue.Trim('"');
                    return registryPath.Equals(currentPath, StringComparison.OrdinalIgnoreCase);
                }

                return false;
            }
        }
        catch { return false; }
    }

    /// <summary>
    /// 設定為開機自動啟動
    /// </summary>
    public static bool SetStartup()
    {
        try
        {
            // 同樣使用 var
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
            {
                if (key == null) return false;

                // 先取得目前的執行檔路徑，並檢查是否為 null
                var currentPath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentPath)) return false;

                // 為路徑加上引號，以防路徑中包含空格
                string exePathWithQuotes = "\"" + currentPath + "\"";
                key.SetValue(AppName, exePathWithQuotes);
                return true;
            }
        }
        catch { return false; }
    }

    /// <summary>
    /// 移除開機自動啟動設定
    /// </summary>
    public static bool RemoveStartup()
    {
        try
        {
            // 同樣使用 var
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
            {
                if (key == null) return false;

                // 檢查登錄檔中是否存在該值
                if (key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName, false);
                }
                return true;
            }
        }
        catch { return false; }
    }
}