// 檔案：SoftwareInfoWindow.xaml.cs (已修正錯字的版本)

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace collect_all
{
    public partial class SoftwareInfoWindow : Window
    {
        public SoftwareInfoWindow()
        {
            InitializeComponent();
            LoadInfoAndTriggerUpload();
        }

        private void LoadInfoAndTriggerUpload()
        {
            List<Software> installedSoftware = GetInstalledSoftware();
            SoftwareDataGrid.ItemsSource = installedSoftware;
            SoftwareCountTextBlock.Text = $"共找到 {installedSoftware.Count} 個已安裝的軟體";
            _ = SoftwareSend.SendAsync(installedSoftware);
        }

        #region Software Collection Logic
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryInfoKeyW", SetLastError = true)]
        internal static extern int RegQueryInfoKey(IntPtr hKey, StringBuilder lpClass, ref uint lpcchClass, IntPtr lpReserved, out uint lpcSubKeys, out uint lpcbMaxSubKeyLen, out uint lpcbMaxClassLen, out uint lpcValues, out uint lpcbMaxValueNameLen, out uint lpcbMaxValueLen, IntPtr lpSecurityDescriptor, out FILETIME lpftLastWriteTime);
        
        // VVVV 就是修正下面這一行 VVVV
        [StructLayout(LayoutKind.Sequential)] // 原本錯打成 Layout.Sequential
        public struct FILETIME { public uint dwLowDateTime; public uint dwHighDateTime; }

        private List<Software> GetInstalledSoftware()
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

        private void ProcessRegistryKey(RegistryKey? key, List<Software> softwareList)
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
        private string GetRegistryKeyLastWriteTime(RegistryKey key)
        {
            try
            {
                uint classSize = 256;
                StringBuilder className = new StringBuilder((int)classSize);
                RegQueryInfoKey(key.Handle.DangerousGetHandle(), className, ref classSize, IntPtr.Zero, out _, out _, out _, out _, out _, out _, IntPtr.Zero, out FILETIME lastWriteTime);
                long high = lastWriteTime.dwHighDateTime;
                long fileTime = (high << 32) | lastWriteTime.dwLowDateTime;
                return DateTime.FromFileTimeUtc(fileTime).ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
            }
            catch { return string.Empty; }
        }
        #endregion
    }
}