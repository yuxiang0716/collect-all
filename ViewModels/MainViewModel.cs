// 檔案: ViewModels/MainViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using collect_all.Commands;
using collect_all.Models;
using collect_all.Services;
using collect_all.Views;

namespace collect_all.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly SystemInfoService _infoService;
        private readonly SystemMacLoginService _macLoginService;
        private bool _isUpdatingSensors = false;

        private string _userIdentifierText = "正在初始化..."; // 初始文字
        public string UserIdentifierText
        {
            get => _userIdentifierText;
            set { _userIdentifierText = value; OnPropertyChanged(); }
        }

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

        public MainViewModel()
        {
            _infoService = new SystemInfoService();
            _macLoginService = new SystemMacLoginService();

            SystemInfoItems = new ObservableCollection<BasicInfoData>();
            HardwareItems = new ObservableCollection<BasicInfoData>();
            StorageVgaItems = new ObservableCollection<BasicInfoData>();
            TemperatureItems = new ObservableCollection<BasicInfoData>();
            SmartItems = new ObservableCollection<BasicInfoData>();

            _isStartupSet = StartupManager.IsStartupSet();

            RefreshCommand = new RelayCommand(async _ => await UpdateSensorsAsync());
            ShowSoftwareInfoCommand = new RelayCommand(_ => new SoftwareInfoWindow().Show());
            LoginCommand = new RelayCommand(async _ => await RequestMacLoginAsync());

            AuthenticationService.Instance.AuthenticationStateChanged += OnAuthenticationStateChanged;

            // 在背景執行啟動流程
            Task.Run(async () => await StartupFlowAsync());
        }

        private async Task StartupFlowAsync()
        {
            // 1. 執行登入流程
            await RequestMacLoginAsync(onStartup: true);

            // 2. 登入流程結束後，無論結果如何，都載入並顯示資料
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LoadStaticInfoAndSendToDb();
                _ = UpdateSensorsAsync();
            });
        }

        private async Task RequestMacLoginAsync(bool onStartup = false)
        {
            string mac = _macLoginService.GetPrimaryMac();
            if (string.IsNullOrEmpty(mac))
            {
                var user = UIAuthService.ShowLoginDialog();
                await UpdateUserIdentifierText();
                return;
            }

            var (found, macRecord) = await _macLoginService.CheckMacInTableAsync(mac);
            if (found && macRecord != null)
            {
                var user = UIAuthService.ShowLoginDialog(macRecord.User, isUsernameReadOnly: true);
                await UpdateUserIdentifierText();
                return;
            }

            // MAC not in table: require user login to assign.
            var loggedInUser = UIAuthService.ShowLoginDialog();
            if (loggedInUser == null)
            {
                // User cancelled the login dialog.
                await UpdateUserIdentifierText();
                return;
            }

            // User logged in, now try to assign MAC.
            var assignResult = await _macLoginService.AssignMacIfAvailableAsync(loggedInUser.Account, mac);
            if (assignResult.Success)
            {
                // Assignment successful, update UI text.
                await UpdateUserIdentifierText();
                return;
            }

            // Assignment failed (e.g., no empty slot).
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.MessageBox.Show(assignResult.Message ?? "指派失敗", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            });

            // Log the user out and update the UI to reflect the "not logged in" state.
            AuthenticationService.Instance.Logout();
            await UpdateUserIdentifierText();
        }


        private void OnAuthenticationStateChanged(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(async () =>
            {
                await UpdateUserIdentifierText();
                // 登入狀態改變後，重新觸發一次資料載入與傳送
                LoadStaticInfoAndSendToDb();
            });
        }

        private async Task UpdateUserIdentifierText()
        {
            var currentUser = AuthenticationService.Instance.CurrentUser;
            if (currentUser != null)
            {
                string mac = _macLoginService.GetPrimaryMac();
                var (_, macRecord) = await _macLoginService.CheckMacInTableAsync(mac);
                string deviceId = macRecord?.DeviceId ?? "N/A";
                UserIdentifierText = $"裝置編號: {deviceId} / 帳號: {currentUser.Account}";
            }
            else
            {
                UserIdentifierText = "未登入，資料未上傳";
            }
        }

        private void LoadStaticInfoAndSendToDb()
        {
            // --- 靜態資訊收集 ---
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

            // --- 資料傳送邏輯 ---
            if (AuthenticationService.Instance.CurrentUser != null)
            {
                var systemInfoList = new List<SystemInfoEntry>();
                foreach (var item in SystemInfoItems) { systemInfoList.Add(new SystemInfoEntry { Category = "系統基本資訊", Item = item.Name, Value = item.Value }); }
                foreach (var item in HardwareItems) { systemInfoList.Add(new SystemInfoEntry { Category = "核心硬體規格", Item = item.Name, Value = item.Value }); }
                foreach (var item in StorageVgaItems) { systemInfoList.Add(new SystemInfoEntry { Category = "顯示與儲存", Item = item.Name, Value = item.Value }); }

                _ = DataSendService.SendSystemInfoAsync(systemInfoList);
            }
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

        public override void Dispose()
        {
            AuthenticationService.Instance.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            _infoService.Dispose();
            base.Dispose();
        }
    }
}