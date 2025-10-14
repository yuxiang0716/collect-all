    // 檔案: ViewModels/MainViewModel.cs (已加入自動傳送功能)

    using System;
    using System.Collections.Generic; // <--- 新增
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
            
            private string _deviceId;
            public string DeviceId
            {
                get => _deviceId;
                set { _deviceId = value; OnPropertyChanged(); }
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
            public ICommand RegisterCommand { get; }

            public MainViewModel()
            {
                _infoService = new SystemInfoService();
                SystemInfoItems = new ObservableCollection<BasicInfoData>();
                HardwareItems = new ObservableCollection<BasicInfoData>();
                StorageVgaItems = new ObservableCollection<BasicInfoData>();
                TemperatureItems = new ObservableCollection<BasicInfoData>();
                SmartItems = new ObservableCollection<BasicInfoData>();
                
                DeviceId = "系統未註冊(登入)";
    
                _isStartupSet = StartupManager.IsStartupSet();

                RefreshCommand = new RelayCommand(async _ => await UpdateSensorsAsync());
                ShowSoftwareInfoCommand = new RelayCommand(_ => new SoftwareInfoWindow().Show());
                
                LoginCommand = new RelayCommand(_ =>
                {
                    var loginWindow = new LoginWindow();
                    // 檢查登入視窗的回傳結果
                    if (loginWindow.ShowDialog() == true)
                    {
                        // 如果登入成功，就去非同步抓取設備編號
                        _ = FetchDeviceIdAsync();
                    }
                });

                RegisterCommand = new RelayCommand(_ =>
                {
                    var registerWindow = new RegisterWindow();
                    registerWindow.ShowDialog();
                });

            // 載入所有初始資訊，並在背景自動傳送
            LoadStaticInfoAndSendToDb();
                _ = UpdateSensorsAsync();
            }

            // *** 修改點：重新命名，並在結尾加入傳送到資料庫的邏輯 ***
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

                // *** 新增的邏輯：將剛剛收集到的所有資訊打包，並呼叫服務傳送 ***
                var systemInfoList = new List<SystemInfoEntry>();
                foreach (var item in SystemInfoItems) { systemInfoList.Add(new SystemInfoEntry { Category = "系統基本資訊", Item = item.Name, Value = item.Value }); }
                foreach (var item in HardwareItems) { systemInfoList.Add(new SystemInfoEntry { Category = "核心硬體規格", Item = item.Name, Value = item.Value }); }
                foreach (var item in StorageVgaItems) { systemInfoList.Add(new SystemInfoEntry { Category = "顯示與儲存", Item = item.Name, Value = item.Value }); }
                
                // 在背景執行緒中非同步傳送，不影響 UI
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
            
            
            private async Task FetchDeviceIdAsync()
        {
            // 步驟 1: 顯示正在載入的訊息
            DeviceId = "正在從資料庫同步設備編號...";

            // --- 以下是模擬與資料庫溝通的程式碼 ---
            // --- 資料庫人員請在此處替換為真實的資料庫查詢邏輯 ---

            // 模擬網路或資料庫查詢的延遲
            await Task.Delay(1500); 

            // 模擬從資料庫成功取回的設備編號
            string idFromDb = $"DEV-YX-{DateTime.Now:yyyyMMdd}"; 
            
            // --- 模擬結束 ---

            // 步驟 2: 更新 UI 上的設備編號
            DeviceId = $"設備編號: {idFromDb}";
        }


            public override void Dispose()
            {
                _infoService.Dispose();
                base.Dispose();
            }
        }
    }