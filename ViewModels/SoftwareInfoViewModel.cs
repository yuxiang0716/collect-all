// 檔案: ViewModels/SoftwareInfoViewModel.cs
// 職責: 擔任 SoftwareInfoWindow 的大腦，處理軟體資訊的收集、顯示和傳送。

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using collect_all.Models;
using collect_all.Services;

namespace collect_all.ViewModels
{
    public class SoftwareInfoViewModel : ViewModelBase
    {
        // --- 綁定到 UI 的屬性 ---
        private ObservableCollection<Software> _installedSoftware;
        public ObservableCollection<Software> InstalledSoftware
        {
            get => _installedSoftware;
            set { _installedSoftware = value; OnPropertyChanged(); }
        }

        private string _softwareCount;
        public string SoftwareCount
        {
            get => _softwareCount;
            set { _softwareCount = value; OnPropertyChanged(); }
        }

        // --- 建構函式 ---
        public SoftwareInfoViewModel()
        {
            _installedSoftware = new ObservableCollection<Software>();
            _softwareCount = "正在載入軟體資訊...";
            LoadAndSendSoftwareInfo();
        }
        
        // --- 私有方法 ---
        private void LoadAndSendSoftwareInfo()
        {
            Task.Run(() =>
            {
                try
                {
                    List<Software> collectedSoftware = GetSoftwareFromRegistry();
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        InstalledSoftware.Clear();
                        foreach (var software in collectedSoftware)
                        {
                            InstalledSoftware.Add(software);
                        }
                        SoftwareCount = $"共找到 {InstalledSoftware.Count} 個已安裝的軟體";
                    });
                    
                    if (collectedSoftware.Count > 0)
                    {
                        _ = DataSendService.SendSoftwareAsync(collectedSoftware);
                        Console.WriteLine("軟體資訊已在背景自動傳送到資料庫。");
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        SoftwareCount = $"載入錯誤: {ex.Message}";
                        System.Windows.MessageBox.Show($"自動處理軟體資訊時發生錯誤：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }

        #region Software Collection Logic (這部分是從服務搬過來的，也可以建立一個 SoftwareService)
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryInfoKeyW", SetLastError = true)]
        internal static extern int RegQueryInfoKey(IntPtr hKey, StringBuilder lpClass, ref uint lpcchClass, IntPtr lpReserved, out uint lpcSubKeys, out uint lpcbMaxSubKeyLen, out uint lpcbMaxClassLen, out uint lpcValues, out uint lpcbMaxValueNameLen, out uint lpcbMaxValueLen, IntPtr lpSecurityDescriptor, out FILETIME lpftLastWriteTime);
        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME { public uint dwLowDateTime; public uint dwHighDateTime; }

        private List<Software> GetSoftwareFromRegistry()
        {
            var softwareList = new List<Software>();
            using (var key64 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")) { ProcessRegistryKey(key64, softwareList); }
            if (Environment.Is64BitOperatingSystem) { using (var key32 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall")) { ProcessRegistryKey(key32, softwareList); } }
            var distinctList = new List<Software>();
            var seen = new HashSet<string>();
            foreach (var software in softwareList)
            {
                string identifier = $"{software.DisplayName}_{software.DisplayVersion}";
                if (!string.IsNullOrEmpty(software.DisplayName) && !seen.Contains(identifier)) { seen.Add(identifier); distinctList.Add(software); }
            }
            for (int i = 0; i < distinctList.Count; i++) { distinctList[i].Number = i + 1; }
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
                            softwareList.Add(new Software { DisplayName = displayName, Publisher = subkey.GetValue("Publisher") as string ?? string.Empty, InstallDate = subkey.GetValue("InstallDate") as string ?? string.Empty, DisplayVersion = subkey.GetValue("DisplayVersion") as string ?? string.Empty, LastUpdate = GetRegistryKeyLastWriteTime(subkey) });
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
                // *** 修改點：只顯示年月日 ***
                return DateTime.FromFileTimeUtc(fileTime).ToLocalTime().ToString("yyyy/MM/dd");
            }
            catch { return string.Empty; }
        }
        #endregion
    }
}