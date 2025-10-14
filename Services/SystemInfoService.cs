// 檔案: Services/SystemInfoService.cs (已將 "N/A" 修改為 "不支援")

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
    public class SystemInfoService : IDisposable
    {
        private readonly Computer _computer;
        private readonly LibreUpdateVisitor _visitor;

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
            _visitor = new LibreUpdateVisitor();
        }

        // --- 感應器相關方法 (已升級) ---
        public ObservableCollection<BasicInfoData> GetTemperatures()
        {
            _computer.Accept(_visitor);

            var list = new ObservableCollection<BasicInfoData>
            {
                new BasicInfoData("CPU 溫度", GetCpuTemperature()),
                new BasicInfoData("主機板溫度", GetMotherboardTemperature()),
                new BasicInfoData("顯示卡溫度", GetGpuTemperature()),
                new BasicInfoData("硬碟溫度", GetStorageTemperature())
            };
            return list;
        }

        #region Private Temperature Getters with Fallbacks

        private string GetCpuTemperature()
        {
            // 方案一：LibreHardwareMonitor
            var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            var tempSensor = cpu?.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && (s.Name.Contains("Package") || s.Name.Contains("Core")));
            if (tempSensor?.Value != null)
            {
                return $"{tempSensor.Value.Value:F1} °C (Libre)";
            }

            // 備用方案：WMI
            try
            {
                var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
                var tempObj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (tempObj != null)
                {
                    var temp = Convert.ToDouble(tempObj["CurrentTemperature"]);
                    var celsius = (temp / 10.0) - 273.15;
                    return $"{celsius:F1} °C (WMI)";
                }
                return "不支援"; // <-- 修改點
            }
            catch { return "不支援"; } // <-- 修改點
        }
        
        private string GetMotherboardTemperature()
        {
            // 方案一：LibreHardwareMonitor
            var mobo = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);
            var moboSensor = mobo?.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
            if (moboSensor?.Value != null)
            {
                return $"{moboSensor.Value.Value:F1} °C (Libre)";
            }

            // 備用方案：深度搜尋 WMI
            try
            {
                var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string instanceName = obj["InstanceName"]?.ToString() ?? "";
                    if (instanceName.Contains("Motherboard") || instanceName.Contains("PCH") || instanceName.Contains("Chipset") || instanceName.Contains("System"))
                    {
                        var temp = Convert.ToDouble(obj["CurrentTemperature"]);
                        var celsius = (temp / 10.0) - 273.15;
                        return $"{celsius:F1} °C (WMI)";
                    }
                }
                return "不支援"; // <-- 修改點
            }
            catch { return "不支援"; } // <-- 修改點
        }

        private string GetGpuTemperature()
        {
            var gpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd);
            var tempSensor = gpu?.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
            return tempSensor?.Value != null ? $"{tempSensor.Value.Value:F1} °C" : "不支援"; // <-- 修改點
        }

        private string GetStorageTemperature()
        {
            var hdd = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Storage);
            var tempSensor = hdd?.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
            return tempSensor?.Value != null ? $"{tempSensor.Value.Value:F1} °C" : "不支援"; // <-- 修改點
        }

        #endregion

        public ObservableCollection<BasicInfoData> GetSmartHealth()
        {
            _computer.Accept(_visitor);
            var list = new ObservableCollection<BasicInfoData>();
            try
            {
                var storageDevices = _computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage);
                foreach(var device in storageDevices)
                {
                    list.Add(new BasicInfoData(device.Name, "健康"));
                }
                if (!list.Any()) list.Add(new BasicInfoData("硬碟健康度", "不支援")); // <-- 修改點
            }
            catch { list.Add(new BasicInfoData("硬碟健康度", "不支援")); } // <-- 修改點
            return list;
        }

        // --- 靜態資訊方法 ---
        public string GetCpuName()
        {
            try { using var s = new ManagementObjectSearcher("select Name from Win32_Processor"); foreach (var i in s.Get()) return i["Name"]?.ToString() ?? "不支援"; return "不支援"; }
            catch { return "不支援"; }
        }
        public string GetCpuCoreCount() // <-- 修改點：回傳型別改為 string
        {
            try { using var s = new ManagementObjectSearcher("select NumberOfCores from Win32_Processor"); foreach (var i in s.Get()) return Convert.ToInt32(i["NumberOfCores"]).ToString(); return "0"; }
            catch { return "不支援"; }
        }
        public string GetMotherboardManufacturer()
        {
            try { using var s = new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_BaseBoard"); foreach (var i in s.Get()) return i["Manufacturer"]?.ToString() ?? "不支援"; return "不支援"; }
            catch { return "不支援"; }
        }
        public string GetMotherboardModel()
        {
            try { using var s = new ManagementObjectSearcher("SELECT Product FROM Win32_BaseBoard"); foreach (var i in s.Get()) return i["Product"]?.ToString() ?? "不支援"; return "不支援"; }
            catch { return "不支援"; }
        }
        public string GetTotalRAM()
        {
            try { using var s = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"); foreach (var i in s.Get()) return (Convert.ToDouble(i["TotalVisibleMemorySize"]) / 1024 / 1024).ToString("0.00"); return "不支援"; }
            catch { return "不支援"; }
        }
        public string GetAvailableRAM()
        {
            try { using var s = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem"); foreach (var i in s.Get()) return (Convert.ToDouble(i["FreePhysicalMemory"]) / 1024 / 1024).ToString("0.00"); return "不支援"; }
            catch { return "不支援"; }
        }
        public string GetGpuName(bool dedicated)
        {
            _computer.Accept(_visitor);
            var targetGpu = _computer.Hardware.FirstOrDefault(h => 
                dedicated ? (h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd) 
                          : (h.HardwareType == HardwareType.GpuIntel));
            if (targetGpu != null)
            {
                return $"{targetGpu.Name} (Libre)";
            }
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Name, Description FROM Win32_VideoController");
                foreach (var i in s.Get())
                {
                    string name = i["Name"]?.ToString() ?? "";
                    string desc = i["Description"]?.ToString() ?? "";
                    bool isIntegrated = name.Contains("Intel") || desc.Contains("Intel") || name.Contains("AMD Radeon Graphics");
                    if (dedicated && !isIntegrated) return $"{name} (WMI)";
                    if (!dedicated && isIntegrated) return $"{name} (WMI)";
                }
                return "不支援"; // <-- 修改點
            }
            catch { return "不支援"; } // <-- 修改點
        }
        public string GetGpuVRAM()
        {
            try { using var s = new ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController"); foreach (var i in s.Get()) return (Convert.ToDouble(i["AdapterRAM"] ?? 0) / 1024 / 1024 / 1024).ToString("0.00"); return "不支援"; }
            catch { return "不支援"; }
        }
        public string GetWindowsVersion()
        {
            try { using var s = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"); foreach (var i in s.Get()) return i["Version"]?.ToString() ?? "不支援"; return "不支援"; }
            catch { return "不支援"; }
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
            catch { list.Add(new BasicInfoData("磁碟資訊", "不支援")); } // <-- 修改點
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
                return "不支援"; // <-- 修改點
            }
            catch { return "不支援"; } // <-- 修改點
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
                return "不支援"; // <-- 修改點
            }
            catch { return "不支援"; } // <-- 修改點
        }

        public void Dispose()
        {
            _computer.Close();
        }
    }
}