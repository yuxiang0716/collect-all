// 檔案: Services/SoftwareCollectionService.cs
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using collect_all.Models;

namespace collect_all.Services
{
    /// <summary>
    /// 軟體收集服務 - 從註冊表讀取已安裝的軟體清單
    /// </summary>
    public class SoftwareCollectionService
    {
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryInfoKeyW", SetLastError = true)]
        internal static extern int RegQueryInfoKey(IntPtr hKey, StringBuilder lpClass, ref uint lpcchClass, IntPtr lpReserved, out uint lpcSubKeys, out uint lpcbMaxSubKeyLen, out uint lpcbMaxClassLen, out uint lpcValues, out uint lpcbMaxValueNameLen, out uint lpcbMaxValueLen, IntPtr lpSecurityDescriptor, out FILETIME lpftLastWriteTime);
        
        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME { public uint dwLowDateTime; public uint dwHighDateTime; }

        public List<Software> GetSoftwareFromRegistry()
        {
            LogService.Log("[SoftwareCollectionService] 開始從註冊表收集軟體清單...");
            
            var softwareList = new List<Software>();
            
            // 讀取 64 位元軟體
            using (var key64 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                ProcessRegistryKey(key64, softwareList);
            }
            
            // 讀取 32 位元軟體（如果是 64 位元系統）
            if (Environment.Is64BitOperatingSystem)
            {
                using (var key32 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    ProcessRegistryKey(key32, softwareList);
                }
            }
            
            int originalCount = softwareList.Count;
            
            // 使用 Dictionary 確保唯一性（最可靠的方式）
            var uniqueSoftware = new Dictionary<string, Software>();
            int skippedEmpty = 0;
            int skippedDuplicate = 0;
            
            foreach (var software in softwareList)
            {
                // 清理名稱
                string cleanedName = software.DisplayName?.Trim() ?? string.Empty;
                
                // 跳過空白名稱
                if (string.IsNullOrWhiteSpace(cleanedName))
                {
                    skippedEmpty++;
                    continue;
                }
                
                // 清理版本
                string cleanedVersion = NormalizeVersion(software.DisplayVersion);
                
                // 建立唯一 key：名稱 + 版本
                string uniqueKey = $"{cleanedName}|{cleanedVersion}";
                
                // 如果已存在，跳過
                if (uniqueSoftware.ContainsKey(uniqueKey))
                {
                    skippedDuplicate++;
                    LogService.Log($"[SoftwareCollectionService] 跳過重複軟體: {cleanedName} (版本: {cleanedVersion})");
                    continue;
                }
                
                // 更新為清理後的值
                software.DisplayName = cleanedName;
                software.DisplayVersion = cleanedVersion;
                
                uniqueSoftware[uniqueKey] = software;
            }
            
            var distinctList = uniqueSoftware.Values.ToList();
            
            LogService.Log($"[SoftwareCollectionService] 收集完成:");
            LogService.Log($"  - 原始資料: {originalCount} 個");
            LogService.Log($"  - 跳過空白: {skippedEmpty} 個");
            LogService.Log($"  - 跳過重複: {skippedDuplicate} 個");
            LogService.Log($"  - 最終結果: {distinctList.Count} 個");
            
            return distinctList;
        }

        private static void ProcessRegistryKey(RegistryKey? key, List<Software> softwareList)
        {
            if (key == null) return;
            
            foreach (string subkeyName in key.GetSubKeyNames())
            {
                using (RegistryKey? subkey = key.OpenSubKey(subkeyName))
                {
                    if (subkey != null)
                    {
                        var displayName = subkey.GetValue("DisplayName") as string;
                        if (!string.IsNullOrEmpty(displayName))
                        {
                            softwareList.Add(new Software
                            {
                                DisplayName = displayName,
                                Publisher = subkey.GetValue("Publisher") as string ?? string.Empty,
                                InstallDate = subkey.GetValue("InstallDate") as string ?? string.Empty,
                                DisplayVersion = subkey.GetValue("DisplayVersion") as string ?? string.Empty,
                                LastUpdate = GetRegistryKeyLastWriteTime(subkey)
                            });
                        }
                    }
                }
            }
        }

        private static string GetRegistryKeyLastWriteTime(RegistryKey key)
        {
            try
            {
                uint classSize = 256;
                StringBuilder className = new StringBuilder((int)classSize);
                RegQueryInfoKey(key.Handle.DangerousGetHandle(), className, ref classSize, IntPtr.Zero, out _, out _, out _, out _, out _, out _, IntPtr.Zero, out FILETIME lastWriteTime);
                long high = lastWriteTime.dwHighDateTime;
                long fileTime = (high << 32) | lastWriteTime.dwLowDateTime;
                return DateTime.FromFileTimeUtc(fileTime).ToLocalTime().ToString("yyyy/MM/dd");
            }
            catch
            {
                return string.Empty;
            }
        }

        // 標準化版本號：確保不為 null，空值轉為空字串
        private static string NormalizeVersion(string? version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return string.Empty;
            
            var normalized = version.Trim();
            
            // 如果是無效值，返回空字串
            if (normalized.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("不支援", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            
            return normalized;
        }
    }
}
