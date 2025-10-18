// 檔案: Services/StartupManager.cs
using Microsoft.Win32;
using System;
using System.IO;

namespace collect_all.Services // <-- 更新命名空間
{
    public static class StartupManager
    {
        private const string AppName = "鴻盛資訊維護服務識別器";
        private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsStartupSet()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                {
                    if (key == null) return false;
                    if (key.GetValue(AppName) is string pathValue)
                    {
                        var currentPath = Environment.ProcessPath;
                        if (string.IsNullOrEmpty(currentPath)) return false;
                        string registryPath = pathValue.Trim('"');
                        return registryPath.Equals(currentPath, StringComparison.OrdinalIgnoreCase);
                    }
                    return false;
                }
            }
            catch { return false; }
        }

        public static bool SetStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                {
                    if (key == null) return false;
                    var currentPath = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(currentPath)) return false;
                    string exePathWithQuotes = "\"" + currentPath + "\"";
                    key.SetValue(AppName, exePathWithQuotes);
                    return true;
                }
            }
            catch { return false; }
        }

        public static bool RemoveStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
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
}