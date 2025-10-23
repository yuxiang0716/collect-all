// �ɮ�: Services/SoftwareCollectionService.cs
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
    /// �n�馬���A�� - �q���U��Ū���w�w�˪��n��M��
    /// </summary>
    public class SoftwareCollectionService
    {
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryInfoKeyW", SetLastError = true)]
        internal static extern int RegQueryInfoKey(IntPtr hKey, StringBuilder lpClass, ref uint lpcchClass, IntPtr lpReserved, out uint lpcSubKeys, out uint lpcbMaxSubKeyLen, out uint lpcbMaxClassLen, out uint lpcValues, out uint lpcbMaxValueNameLen, out uint lpcbMaxValueLen, IntPtr lpSecurityDescriptor, out FILETIME lpftLastWriteTime);
        
        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME { public uint dwLowDateTime; public uint dwHighDateTime; }

        public List<Software> GetSoftwareFromRegistry()
        {
            LogService.Log("[SoftwareCollectionService] �}�l�q���U�����n��M��...");
            
            var softwareList = new List<Software>();
            
            // Ū�� 64 �줸�n��
            using (var key64 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                ProcessRegistryKey(key64, softwareList);
            }
            
            // Ū�� 32 �줸�n��]�p�G�O 64 �줸�t�Ρ^
            if (Environment.Is64BitOperatingSystem)
            {
                using (var key32 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    ProcessRegistryKey(key32, softwareList);
                }
            }
            
            int originalCount = softwareList.Count;
            
            // �ϥ� Dictionary �T�O�ߤ@�ʡ]�̥i�a���覡�^
            var uniqueSoftware = new Dictionary<string, Software>();
            int skippedEmpty = 0;
            int skippedDuplicate = 0;
            
            foreach (var software in softwareList)
            {
                // �M�z�W��
                string cleanedName = software.DisplayName?.Trim() ?? string.Empty;
                
                // ���L�ťզW��
                if (string.IsNullOrWhiteSpace(cleanedName))
                {
                    skippedEmpty++;
                    continue;
                }
                
                // �M�z����
                string cleanedVersion = NormalizeVersion(software.DisplayVersion);
                
                // �إ߰ߤ@ key�G�W�� + ����
                string uniqueKey = $"{cleanedName}|{cleanedVersion}";
                
                // �p�G�w�s�b�A���L
                if (uniqueSoftware.ContainsKey(uniqueKey))
                {
                    skippedDuplicate++;
                    LogService.Log($"[SoftwareCollectionService] ���L���Ƴn��: {cleanedName} (����: {cleanedVersion})");
                    continue;
                }
                
                // ��s���M�z�᪺��
                software.DisplayName = cleanedName;
                software.DisplayVersion = cleanedVersion;
                
                uniqueSoftware[uniqueKey] = software;
            }
            
            var distinctList = uniqueSoftware.Values.ToList();
            
            LogService.Log($"[SoftwareCollectionService] ��������:");
            LogService.Log($"  - ��l���: {originalCount} ��");
            LogService.Log($"  - ���L�ť�: {skippedEmpty} ��");
            LogService.Log($"  - ���L����: {skippedDuplicate} ��");
            LogService.Log($"  - �̲׵��G: {distinctList.Count} ��");
            
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

        // �зǤƪ������G�T�O���� null�A�ŭ��ର�Ŧr��
        private static string NormalizeVersion(string? version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return string.Empty;
            
            var normalized = version.Trim();
            
            // �p�G�O�L�ĭȡA��^�Ŧr��
            if (normalized.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("���䴩", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            
            return normalized;
        }
    }
}
