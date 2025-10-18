// 檔案: ViewModels/MainViewModel.cs (已更新)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using collect_all.Commands;
using collect_all.Models;
using collect_all.Services;
using collect_all.Views;

namespace collect_all.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly SystemInfoService _infoService;
        private bool _isUpdatingSensors = false;

        // --- 屬性修改 ---
        // 舊的 DeviceId 和 LoginStatusText 已被移除
        // 新增一個屬性來統一處理左上角的顯示文字
        private string _userIdentifierText = string.Empty;
        public string UserIdentifierText
        {
            get => _userIdentifierText;
            set { _userIdentifierText = value; OnPropertyChanged(); }
        }
        // --- 修改結束 ---

        public ObservableCollection<BasicInfoData> SystemInfoItems { get; set; }
        public ObservableCollection<BasicInfoData> HardwareItems { get; set; }
        public ObservableCollection<BasicInfoData> StorageVgaItems { get; set; }
        public ObservableCollection<BasicInfoData> TemperatureItems { get; set; }
        public ObservableCollection<BasicInfoData> SmartItems { get; set; }

        private bool _isStartupSet;
        public bool IsStartupSet
        {
            get => _isStartupSet;
            set
            {
                if (_isStartupSet != value)
                {
                    _isStartupSet = value;
                    OnPropertyChanged();
                    if (value) StartupManager.SetStartup();
                    else StartupManager.RemoveStartup();
                }
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ShowSoftwareInfoCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand RegisterCommand { get; }

        public MainViewModel()
        {
            _infoService = new SystemInfoService();
            SystemInfoItems = new ObservableCollection<BasicInfoData>();
            HardwareItems = new ObservableCollection<BasicInfoData>();
            StorageVgaItems = new ObservableCollection<BasicInfoData>();
            TemperatureItems = new ObservableCollection<BasicInfoData>();
            SmartItems = new ObservableCollection<BasicInfoData>();

            _isStartupSet = StartupManager.IsStartupSet();

            RefreshCommand = new RelayCommand(async _ => await UpdateSensorsAsync());
            ShowSoftwareInfoCommand = new RelayCommand(_ => new SoftwareInfoWindow().Show());
            LoginCommand = new RelayCommand(_ => new LoginWindow().ShowDialog());
            RegisterCommand = new RelayCommand(_ => new RegisterWindow().ShowDialog());

            AuthenticationService.Instance.AuthenticationStateChanged += OnAuthenticationStateChanged;

            // --- 邏輯修改 ---
            // 呼叫新的方法來初始化顯示文字
            UpdateUserIdentifierText();
            // 舊的 FetchDeviceIdAsync 已被移除
            // --- 修改結束 ---

            LoadStaticInfoAndSendToDb();
            _ = UpdateSensorsAsync();
        }

        private void OnAuthenticationStateChanged(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // 當登入/登出狀態改變時，更新顯示文字
                UpdateUserIdentifierText();
            });
        }

        // --- 方法更新 ---
        // 這個新方法取代了舊的 UpdateLoginStatusText
        private void UpdateUserIdentifierText()
        {
            var currentUser = AuthenticationService.Instance.CurrentUser;
            if (currentUser != null)
            {
                UserIdentifierText = $"設備編號(帳號): {currentUser.Username}";
            }
            else
            {
                UserIdentifierText = "設備編號(帳號): (未登入)";
            }
        }
        // --- 更新結束 ---

        private void LoadStaticInfoAndSendToDb()
        {
            SystemInfoItems.Clear();
            HardwareItems.Clear();
            StorageVgaItems.Clear();

            SystemInfoItems.Add(new BasicInfoData("電腦名稱", Environment.MachineName));
            SystemInfoItems.Add(new BasicInfoData("作業系統", Environment.OSVersion.ToString()));
            SystemInfoItems.Add(new BasicInfoData("Windows 版本", _infoService.GetWindowsVersion()));
            SystemInfoItems.Add(new BasicInfoData("網路 IP", _infoService.GetLocalIPv4()));
            SystemInfoItems.Add(new BasicInfoData("MAC 位址", _infoService.GetMacAddress()));

            HardwareItems.Add(new BasicInfoData("CPU 型號", _infoService.GetCpuName()));
            HardwareItems.Add(new BasicInfoData("CPU 核心數", _infoService.GetCpuCoreCount()));
            HardwareItems.Add(new BasicInfoData("主機板製造商", _infoService.GetMotherboardManufacturer()));
            HardwareItems.Add(new BasicInfoData("主機板型號", _infoService.GetMotherboardModel()));
            HardwareItems.Add(new BasicInfoData("記憶體總容量 (GB)", _infoService.GetTotalRAM()));
            HardwareItems.Add(new BasicInfoData("記憶體剩餘容量 (GB)", _infoService.GetAvailableRAM()));

            StorageVgaItems.Add(new BasicInfoData("獨立顯示卡 (GPU)", _infoService.GetGpuName(true)));
            StorageVgaItems.Add(new BasicInfoData("內建顯示卡", _infoService.GetGpuName(false)));
            StorageVgaItems.Add(new BasicInfoData("顯示卡 VRAM (GB)", _infoService.GetGpuVRAM()));
            var allDrives = _infoService.GetAllDrivesInfo();
            foreach (var d in allDrives)
                StorageVgaItems.Add(d);

            var systemInfoList = new List<SystemInfoEntry>();
            foreach (var item in SystemInfoItems) { systemInfoList.Add(new SystemInfoEntry { Category = "系統基本資訊", Item = item.Name, Value = item.Value }); }
            foreach (var item in HardwareItems) { systemInfoList.Add(new SystemInfoEntry { Category = "核心硬體規格", Item = item.Name, Value = item.Value }); }
            foreach (var item in StorageVgaItems) { systemInfoList.Add(new SystemInfoEntry { Category = "顯示與儲存", Item = item.Name, Value = item.Value }); }

            _ = DataSendService.SendSystemInfoAsync(systemInfoList);
        }

        private async Task UpdateSensorsAsync()
        {
            if (_isUpdatingSensors) return;
            _isUpdatingSensors = true;

            var temps = await Task.Run(() => _infoService.GetTemperatures());
            var smarts = await Task.Run(() => _infoService.GetSmartHealth());

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                TemperatureItems.Clear();
                foreach (var item in temps) TemperatureItems.Add(item);
                SmartItems.Clear();
                foreach (var item in smarts) SmartItems.Add(item);
            });

            _isUpdatingSensors = false;
        }

        // --- 移除 FetchDeviceIdAsync 方法 ---

        public override void Dispose()
        {
            AuthenticationService.Instance.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            _infoService.Dispose();
            base.Dispose();
        }
    }
}