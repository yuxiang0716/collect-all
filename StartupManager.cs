// 檔案: StartupManager.cs
using Microsoft.Win32;
using System;

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
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                if (key == null) return false;
                object value = key.GetValue(AppName);
                if (value == null) return false;
                
                // 移除路徑中的引號以進行比較
                string registryPath = value.ToString().Trim('"');
                return registryPath.Equals(Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
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
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
            {
                if (key == null) return false;
                
                // 為路徑加上引號，以防路徑中包含空格
                string exePathWithQuotes = "\"" + Environment.ProcessPath + "\"";
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
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
            {
                if (key == null) return false;
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