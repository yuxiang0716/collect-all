using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms; // Tray Icon
using System.Drawing;      // Icon
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace collect_all
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<BasicInfoData> SystemInfoItems { get; set; }
        public ObservableCollection<BasicInfoData> HardwareItems { get; set; }
        public ObservableCollection<BasicInfoData> StorageVgaItems { get; set; }
        public ObservableCollection<BasicInfoData> TemperatureItems { get; set; }
        public ObservableCollection<BasicInfoData> SmartItems { get; set; }

        private Computer _computer;
        private NotifyIcon m_notifyIcon;
        private WindowState m_storedWindowState = WindowState.Normal;

        public MainWindow()
        {
            InitializeComponent();

            StartupCheckBox.IsChecked = StartupManager.IsStartupSet();

            this.WindowState = WindowState.Minimized;
            this.Hide();

            SystemInfoItems = new ObservableCollection<BasicInfoData>();
            HardwareItems = new ObservableCollection<BasicInfoData>();
            StorageVgaItems = new ObservableCollection<BasicInfoData>();
            TemperatureItems = new ObservableCollection<BasicInfoData>();
            SmartItems = new ObservableCollection<BasicInfoData>();
            DataContext = this;

            _computer = new Computer()
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true
            };
            _computer.Open();
            
            m_notifyIcon = new NotifyIcon
            {
                BalloonTipText = "The app has been minimised. Click the tray icon to show.",
                BalloonTipTitle = "系統資訊收集器",
                Text = "系統資訊收集器"
            };
            try
            {
                m_notifyIcon.Icon = new Icon("TheAppIcon.ico");
            }
            catch { }
            m_notifyIcon.MouseClick += m_notifyIcon_MouseClick;

            var contextMenu = new ContextMenuStrip();
            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (s, e) =>
            {
                m_notifyIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
            };
            contextMenu.Items.Add(exitItem);
            m_notifyIcon.ContextMenuStrip = contextMenu;
            
            this.StateChanged += OnStateChanged;
            this.IsVisibleChanged += OnIsVisibleChanged;
            this.Closing += OnClose;
            
            CollectStaticInfo();
            UpdateSensors();

            Task.Run(() =>
            {
                while (true)
                {
                    UpdateSensors();
                    Thread.Sleep(600000);
                }
            });
            
            this.WindowStyle = WindowStyle.SingleBorderWindow;
            this.ResizeMode = ResizeMode.CanMinimize;

            m_notifyIcon.Visible = true;
        }

        private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (StartupCheckBox.IsChecked == true)
            {
                if (!StartupManager.SetStartup())
                {
                    System.Windows.MessageBox.Show("設定開機啟動失敗！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                if (!StartupManager.RemoveStartup())
                {
                    System.Windows.MessageBox.Show("移除開機啟動失敗！", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #region Tray Events
        private void OnClose(object? sender, System.ComponentModel.CancelEventArgs args)
        {
            args.Cancel = true;
            WindowState = WindowState.Minimized;
            Hide();
            ShowTrayIcon(true);
        }

        private void OnStateChanged(object? sender, EventArgs args)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                ShowTrayIcon(true);
                m_notifyIcon.ShowBalloonTip(2000);
            }
            else
            {
                m_storedWindowState = WindowState;
            }
        }

        private void OnIsVisibleChanged(object? sender, DependencyPropertyChangedEventArgs args)
        {
            ShowTrayIcon(!IsVisible);
        }

        private void m_notifyIcon_Click(object? sender, EventArgs e)
        {
            Show();
            WindowState = m_storedWindowState;
            Activate();
        }

        private void m_notifyIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Show();
                WindowState = m_storedWindowState;
                Activate();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateSensors();
        }

        private void ShowTrayIcon(bool show)
        {
            if (m_notifyIcon != null)
                m_notifyIcon.Visible = show;
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            new LoginWindow().ShowDialog();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            new RegisterWindow().ShowDialog();
        }
        #endregion

        #region 靜態資訊
        private void CollectStaticInfo()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                SystemInfoItems.Clear();
                SystemInfoItems.Add(new BasicInfoData("電腦名稱", Environment.MachineName));
                SystemInfoItems.Add(new BasicInfoData("作業系統", Environment.OSVersion.ToString()));
                SystemInfoItems.Add(new BasicInfoData("Windows 版本", GetWindowsVersion()));
                SystemInfoItems.Add(new BasicInfoData("網路 IP", GetLocalIPv4()));
                SystemInfoItems.Add(new BasicInfoData("MAC 位址", GetMacAddress()));

                HardwareItems.Clear();
                HardwareItems.Add(new BasicInfoData("CPU 型號", GetCpuName()));
                HardwareItems.Add(new BasicInfoData("CPU 核心數", GetCpuCoreCount().ToString()));
                HardwareItems.Add(new BasicInfoData("主機板製造商", GetMotherboardManufacturer()));
                HardwareItems.Add(new BasicInfoData("主機板型號", GetMotherboardModel()));
                HardwareItems.Add(new BasicInfoData("記憶體總容量 (GB)", GetTotalRAM()));
                HardwareItems.Add(new BasicInfoData("記憶體剩餘容量 (GB)", GetAvailableRAM()));

                StorageVgaItems.Clear();
                StorageVgaItems.Add(new BasicInfoData("獨立顯示卡 (GPU)", GetGpuName(true)));
                StorageVgaItems.Add(new BasicInfoData("內建顯示卡", GetGpuName(false)));
                StorageVgaItems.Add(new BasicInfoData("顯示卡 VRAM (GB)", GetGpuVRAM()));
                var allDrives = GetAllDrivesInfo();
                foreach (var d in allDrives)
                    StorageVgaItems.Add(d);
            });
        }

        private void UpdateSensors()
        {
            var temps = GetTemperatures();
            var smartList = GetSmartHealth();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                TemperatureItems.Clear();
                foreach (var t in temps) TemperatureItems.Add(t);

                SmartItems.Clear();
                foreach (var s in smartList) SmartItems.Add(s);
            });
        }
        #endregion

        #region 溫度 & SMART
        private ObservableCollection<BasicInfoData> GetTemperatures()
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

        private ObservableCollection<BasicInfoData> GetSmartHealth()
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
        #endregion

        #region 靜態硬體資訊方法
        private string GetCpuName()
        {
            try { using var s = new ManagementObjectSearcher("select Name from Win32_Processor"); foreach (var i in s.Get()) return i["Name"]?.ToString() ?? "N/A"; return "N/A"; }
            catch { return "N/A"; }
        }

        private int GetCpuCoreCount()
        {
            try { using var s = new ManagementObjectSearcher("select NumberOfCores from Win32_Processor"); foreach (var i in s.Get()) return Convert.ToInt32(i["NumberOfCores"]); return 0; }
            catch { return 0; }
        }

        private string GetMotherboardManufacturer()
        {
            try { using var s = new ManagementObjectSearcher("SELECT Manufacturer FROM Win32_BaseBoard"); foreach (var i in s.Get()) return i["Manufacturer"]?.ToString() ?? "N/A"; return "N/A"; }
            catch { return "N/A"; }
        }

        private string GetMotherboardModel()
        {
            try { using var s = new ManagementObjectSearcher("SELECT Product FROM Win32_BaseBoard"); foreach (var i in s.Get()) return i["Product"]?.ToString() ?? "N/A"; return "N/A"; }
            catch { return "N/A"; }
        }

        private string GetTotalRAM()
        {
            try { using var s = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"); foreach (var i in s.Get()) return (Convert.ToDouble(i["TotalVisibleMemorySize"]) / 1024 / 1024).ToString("0.00"); return "N/A"; }
            catch { return "N/A"; }
        }

        private string GetAvailableRAM()
        {
            try { using var s = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem"); foreach (var i in s.Get()) return (Convert.ToDouble(i["FreePhysicalMemory"]) / 1024 / 1024).ToString("0.00"); return "N/A"; }
            catch { return "N/A"; }
        }

        private string GetGpuName(bool dedicated)
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

        private string GetGpuVRAM()
        {
            try { using var s = new ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController"); foreach (var i in s.Get()) return (Convert.ToDouble(i["AdapterRAM"] ?? 0) / 1024 / 1024 / 1024).ToString("0.00"); return "N/A"; }
            catch { return "N/A"; }
        }

        private string GetWindowsVersion()
        {
            try { using var s = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"); foreach (var i in s.Get()) return i["Version"]?.ToString() ?? "N/A"; return "N/A"; }
            catch { return "N/A"; }
        }
        private ObservableCollection<BasicInfoData> GetAllDrivesInfo()
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

        private string GetLocalIPv4()
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

        private string GetMacAddress()
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
        #endregion
    }

    public class BasicInfoData
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public BasicInfoData(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}