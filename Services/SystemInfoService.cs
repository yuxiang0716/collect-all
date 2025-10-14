// 檔案: Services/SystemInfoService.cs
// 職責: 封裝所有與硬體、系統、網路資訊的偵測邏輯。

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using collect_all.Models;
using LibreHardwareMonitor.Hardware;

namespace collect_all.Services
{
    // 修正 #1：在類別名稱後面加上 ": IDisposable"，表示它要實作 IDisposable 介面
    public class SystemInfoService : IDisposable
    {
        private readonly Computer _computer;

        public SystemInfoService()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true
            };
            _computer.Open();
        }

        // --- 感應器相關方法 ---
        public ObservableCollection<BasicInfoData> GetTemperatures()
        {
            var list = new ObservableCollection<BasicInfoData>();
            bool cpuFound = false, gpuFound = false, hddFound = false, mbFound = false;
            try
            {
                foreach (var hw in _computer.Hardware)
                {
                    hw.Update();
                    if (hw.HardwareType == HardwareType.Cpu && !cpuFound)
                    {
                        foreach (var s in hw.Sensors)
                            if (s.SensorType == SensorType.Temperature && s.Value.HasValue)
                            {
                                list.Add(new BasicInfoData("CPU 溫度", $"{s.Value.Value:0.0} °C"));
                                cpuFound = true;
                                break;
                            }
                    }
                    if (hw.HardwareType == HardwareType.Motherboard && !mbFound)
                    {
                        foreach (var s in hw.Sensors)
                            if (s.SensorType == SensorType.Temperature && s.Value.HasValue)
                            {
                                list.Add(new BasicInfoData("主機板溫度", $"{s.Value.Value:0.0} °C"));
                                mbFound = true;
                                break;
                            }
                    }
                    if ((hw.HardwareType == HardwareType.GpuNvidia || hw.HardwareType == HardwareType.GpuAmd || hw.HardwareType == HardwareType.GpuIntel) && !gpuFound)
                    {
                        foreach (var s in hw.Sensors)
                            if (s.SensorType == SensorType.Temperature && s.Value.HasValue)
                            {
                                list.Add(new BasicInfoData("顯示卡溫度", $"{s.Value.Value:0.0} °C"));
                                gpuFound = true;
                                break;
                            }
                    }
                    if (hw.HardwareType == HardwareType.Storage && !hddFound)
                    {
                        foreach (var s in hw.Sensors)
                            if (s.SensorType == SensorType.Temperature && s.Value.HasValue)
                            {
                                list.Add(new BasicInfoData("硬碟溫度", $"{s.Value.Value:0.0} °C"));
                                hddFound = true;
                                break;
                            }
                    }
                }
            }
            catch { }
            if (!cpuFound) list.Add(new BasicInfoData("CPU 溫度", "N/A"));
            if (!mbFound) list.Add(new BasicInfoData("主機板溫度", "N/A"));
            if (!gpuFound) list.Add(new BasicInfoData("顯示卡溫度", "N/A"));
            if (!hddFound) list.Add(new BasicInfoData("硬碟溫度", "N/A"));
            return list;
        }

        public ObservableCollection<BasicInfoData> GetSmartHealth()
        {
            var list = new ObservableCollection<BasicInfoData>();
            try
            {
                foreach (var hw in _computer.Hardware)
                {
                    if (hw.HardwareType == HardwareType.Storage)
                    {
                        hw.Update();
                        list.Add(new BasicInfoData(hw.Name, "健康"));
                    }
                }
                if (!list.Any()) list.Add(new BasicInfoData("硬碟健康度", "N/A"));
            }
            catch { list.Add(new BasicInfoData("硬碟健康度", "N/A")); }
            return list;
        }

        // --- 靜態資訊方法 ---
        public string GetCpuName()
        {
            try { using var s = new ManagementObjectSearcher("select Name from Win32_Processor"); foreach (var i in s.Get()) return i["Name"]?.ToString() ?? "N/A"; return "N/A"; }
            catch { return "N/A"; }
        }

        public int GetCpuCoreCount()
        {
            try { using var s = new ManagementObjectSearcher("select NumberOfCores from Win32_Processor"); foreach (var i in s.Get()) return Convert.ToInt32(i["NumberOfCores"]); return 0; }
            catch { return 0; }
        }

        public string GetMotherboardManufacturer()
        {
            try { using var s = new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_BaseBoard"); foreach (var i in s.Get()) return i["Manufacturer"]?.ToString() ?? "N/A"; return "N/A"; }
            catch { return "N/A"; }
        }

        public string GetMotherboardModel()
        {
            try { using var s = new ManagementObjectSearcher("SELECT Product FROM Win32_BaseBoard"); foreach (var i in s.Get()) return i["Product"]?.ToString() ?? "N/A"; return "N/A"; }
            catch { return "N/A"; }
        }

        public string GetTotalRAM()
        {
            try { using var s = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"); foreach (var i in s.Get()) return (Convert.ToDouble(i["TotalVisibleMemorySize"]) / 1024 / 1024).ToString("0.00"); return "N/A"; }
            catch { return "N/A"; }
        }

        public string GetAvailableRAM()
        {
            try { using var s = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem"); foreach (var i in s.Get()) return (Convert.ToDouble(i["FreePhysicalMemory"]) / 1024 / 1024).ToString("0.00"); return "N/A"; }
            catch { return "N/A"; }
        }

        public string GetGpuName(bool dedicated)
        {
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
                foreach (var i in s.Get())
                {
                    bool isDedicated = Convert.ToUInt32(i["AdapterRAM"] ?? 0) > 0;
                    if (isDedicated == dedicated) return i["Name"]?.ToString() ?? "N/A";
                }
                return "N/A";
            }
            catch { return "N/A"; }
        }

        public string GetGpuVRAM()
        {
            try { using var s = new ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController"); foreach (var i in s.Get()) return (Convert.ToDouble(i["AdapterRAM"] ?? 0) / 1024 / 1024 / 1024).ToString("0.00"); return "N/A"; }
            catch { return "N/A"; }
        }

        public string GetWindowsVersion()
        {
            try { using var s = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"); foreach (var i in s.Get()) return i["Version"]?.ToString() ?? "N/A"; return "N/A"; }
            catch { return "N/A"; }
        }
        public ObservableCollection<BasicInfoData> GetAllDrivesInfo()
        {
            var list = new ObservableCollection<BasicInfoData>();
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    list.Add(new BasicInfoData($"{drive.Name} 總容量 (GB)", (drive.TotalSize / 1024.0 / 1024 / 1024).ToString("0.00")));
                    list.Add(new BasicInfoData($"{drive.Name} 剩餘容量 (GB)", (drive.AvailableFreeSpace / 1024.0 / 1024 / 1024).ToString("0.00")));
                }
            }
            catch { list.Add(new BasicInfoData("磁碟資訊", "N/A")); }
            return list;
        }

        public string GetLocalIPv4()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            return ip.Address.ToString();
                return "N/A";
            }
            catch { return "N/A"; }
        }

        public string GetMacAddress()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up)
                    {
                        var bytes = ni.GetPhysicalAddress().GetAddressBytes();
                        return string.Join("-", Array.ConvertAll(bytes, b => b.ToString("X2")));
                    }
                }
                return "N/A";
            }
            catch { return "N/A"; }
        }

        // 修正 #2：移除 public override void Dispose() 前面的 "override" 關鍵字
        public void Dispose()
        {
            _computer.Close();
        }
    }
}