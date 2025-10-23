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
using System.Threading;
using System.Diagnostics;
using System.Windows.Threading;

namespace collect_all.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly SystemInfoService _infoService;
        private readonly SystemMacLoginService _macLoginService;
        private bool _isUpdatingSensors = false;

        private List<PerformanceCounter> _downloadCounters;
        private List<PerformanceCounter> _uploadCounters;
        private List<string> _counterInstanceNames;
        private DispatcherTimer? _networkTimer;

        private CancellationTokenSource _cts = new CancellationTokenSource();

        private int _networkUpdateIntervalSeconds = 1;   // 網路預設 1 秒
        private int _sensorUpdateIntervalSeconds = 600;  // 感應器預設 600 秒 (10分鐘)
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
        public ObservableCollection<BasicInfoData> UsageItems { get; set; }
        public ObservableCollection<NetworkTrafficData> NetworkTrafficItems { get; set; }
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

        // --- 新增：綁定到設定頁面的網路更新頻率 ---
        public string NetworkUpdateIntervalSeconds
        {
            get => _networkUpdateIntervalSeconds.ToString();
            set
            {
                // 嘗試解析輸入值，必須是大於 0 的整數
                if (int.TryParse(value, out int seconds) && seconds > 0)
                {
                    _networkUpdateIntervalSeconds = seconds;
                    
                    // 立刻更新正在運作的網路計時器
                    if (_networkTimer != null)
                    {
                        _networkTimer.Stop();
                        _networkTimer.Interval = TimeSpan.FromSeconds(_networkUpdateIntervalSeconds);
                        _networkTimer.Start();
                    }
                }
                // 通知 UI 更新 (即使解析失敗，也要讓 TextBox 恢復原狀)
                OnPropertyChanged(); 
            }
        }

        // --- 新增：綁定到設定頁面的感應器更新頻率 ---
        public string SensorUpdateIntervalSeconds
        {
            get => _sensorUpdateIntervalSeconds.ToString();
            set
            {
                if (int.TryParse(value, out int seconds) && seconds > 0)
                {
                    _sensorUpdateIntervalSeconds = seconds;
                    
                    // 感應器任務使用 Task.Delay，我們需要*重啟*任務
                    RestartPeriodicSensorUpdates();
                }
                OnPropertyChanged();
            }
        }




        public ICommand RefreshCommand { get; }
        public ICommand ShowSoftwareInfoCommand { get; }
        public ICommand LoginCommand { get; }

        public ICommand ShowBootHistoryCommand { get; }

        public MainViewModel()
        {
            _infoService = new SystemInfoService();
            _macLoginService = new SystemMacLoginService();

            SystemInfoItems = new ObservableCollection<BasicInfoData>();
            HardwareItems = new ObservableCollection<BasicInfoData>();
            StorageVgaItems = new ObservableCollection<BasicInfoData>();
            TemperatureItems = new ObservableCollection<BasicInfoData>();
            SmartItems = new ObservableCollection<BasicInfoData>();
            UsageItems = new ObservableCollection<BasicInfoData>();


            NetworkTrafficItems = new ObservableCollection<NetworkTrafficData>();
            _downloadCounters = new List<PerformanceCounter>();
            _uploadCounters = new List<PerformanceCounter>();
            _counterInstanceNames = new List<string>();
            InitializeNetworkCounters();
            SetupNetworkTimer();


            _isStartupSet = StartupManager.IsStartupSet();

            RefreshCommand = new RelayCommand(async _ => await UpdateSensorsAsync());
            ShowSoftwareInfoCommand = new RelayCommand(_ => new SoftwareInfoWindow().Show());
            LoginCommand = new RelayCommand(async _ => await RequestMacLoginAsync());

            ShowBootHistoryCommand = new RelayCommand(_ => new Views.BootHistoryWindow().Show());

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
            _ = StartPeriodicUpdatesAsync(_cts.Token);
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

            // 平行抓取溫度、SMART 和使用率
            var tempsTask = Task.Run(() => _infoService.GetTemperatures());
            var smartsTask = Task.Run(() => _infoService.GetSmartHealth());
            var usagesTask = Task.Run(() => _infoService.GetUsage()); // <-- 新增

            await Task.WhenAll(tempsTask, smartsTask, usagesTask); // <-- 等待全部完成

            var temps = await tempsTask;
            var smarts = await smartsTask;
            var usages = await usagesTask; // <-- 取得結果

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                UsageItems.Clear(); // <-- 新增
                foreach (var item in usages) UsageItems.Add(item); // <-- 新增

                TemperatureItems.Clear();
                foreach (var item in temps) TemperatureItems.Add(item);
                SmartItems.Clear();
                foreach (var item in smarts) SmartItems.Add(item);
            });

            _isUpdatingSensors = false;
        }



        private async Task StartPeriodicUpdatesAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // VVVV 修改點 VVVV
                    // await Task.Delay(TimeSpan.FromMinutes(10), token); // 舊的
                    await Task.Delay(TimeSpan.FromSeconds(_sensorUpdateIntervalSeconds), token); // 新的：使用變數
                                                                                                 // ^^^^ 修改點 ^^^^

                    if (token.IsCancellationRequested) break;

                    // 執行刷新 (不需要在 Dispatcher.Invoke 內，UpdateSensorsAsync 內部會處理)
                    await UpdateSensorsAsync();
                }
            }
            catch (TaskCanceledException)
            {
                // 程式關閉時會觸發，屬正常行為
            }
            catch (Exception ex)
            {
                // 紀錄其他可能的錯誤
                Console.WriteLine($"Periodic update error: {ex.Message}");
            }
        }


        private void RestartPeriodicSensorUpdates()
        {
            // 1. 取消並釋放舊的 CancellationTokenSource
            _cts.Cancel();
            _cts.Dispose();

            // 2. 建立一個新的 CancellationTokenSource
            _cts = new CancellationTokenSource();

            // 3. 使用新的 Token 重新啟動背景任務
            _ = StartPeriodicUpdatesAsync(_cts.Token);
        }
    


        public override void Dispose()
        {
            _cts.Cancel(); // <-- 新增：通知計時器停止
            _cts.Dispose(); // <-- 新增：釋放資源

            // --- 新增：停止並釋放網路監控資源 ---
        _networkTimer?.Stop();
        if (_downloadCounters != null)
        {
            foreach (var c in _downloadCounters) c.Dispose();
            _downloadCounters.Clear();
        }
        if (_uploadCounters != null)
        {
            foreach (var c in _uploadCounters) c.Dispose();
            _uploadCounters.Clear();
        }

            AuthenticationService.Instance.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            _infoService.Dispose();
            base.Dispose();
        }
        

        // --- 新增：初始化所有網路卡的效能計數器 ---
    private void InitializeNetworkCounters()
    {
        try
        {
            PerformanceCounterCategory category = new PerformanceCounterCategory("Network Interface");
            string[] instances = category.GetInstanceNames();

            foreach (string instance in instances)
            {
                try
                {
                    var dlCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", instance);
                    var ulCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instance);

                    // 預熱
                    dlCounter.NextValue();
                    ulCounter.NextValue();

                    _downloadCounters.Add(dlCounter);
                    _uploadCounters.Add(ulCounter);
                    _counterInstanceNames.Add(instance);
                }
                catch
                {
                    // 忽略無法讀取的介面 (例如虛擬網卡)
                }
            }
        }
        catch (Exception ex)
        {
            // 無法讀取 PerformanceCounter (可能權限不足)
             System.Windows.MessageBox.Show($"無法載入網路監控功能：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // --- 新增：設定每秒更新的計時器 ---
private void SetupNetworkTimer()
    {
        _networkTimer = new DispatcherTimer
        {
            // VVVV 修改點 VVVV
            //Interval = TimeSpan.FromSeconds(1) // 舊的
            Interval = TimeSpan.FromSeconds(_networkUpdateIntervalSeconds) // 新的：使用變數
            // ^^^^ 修改點 ^^^^
        };
        _networkTimer.Tick += NetworkTimer_Tick;
        _networkTimer.Start();
    }

    // --- 新增：計時器觸發的事件 (核心邏輯) ---
    private void NetworkTimer_Tick(object? sender, EventArgs e)
    {
        // 1. 清空目前的列表
        NetworkTrafficItems.Clear();

        // 2. 遍歷所有計數器
        for (int i = 0; i < _downloadCounters.Count; i++)
        {
            try
            {
                float downloadSpeed = _downloadCounters[i].NextValue();
                float uploadSpeed = _uploadCounters[i].NextValue();

                // 3. 檢查是否有流量 (例如大於 1 KB/s)
                if (downloadSpeed > 1024 || uploadSpeed > 1024)
                {
                    // 4. 如果有流量，才加入到清單中
                    NetworkTrafficItems.Add(new NetworkTrafficData
                    {
                        InterfaceName = _counterInstanceNames[i],
                        DownloadSpeed = FormatSpeed(downloadSpeed),
                        UploadSpeed = FormatSpeed(uploadSpeed)
                    });
                }
            }
            catch
            {
                // 讀取失敗，可能網卡已移除，忽略
            }
        }
    }

    // --- 新增：格式化速度 (Bytes/sec 轉為 KB/s 或 MB/s) ---
    private string FormatSpeed(float bytesPerSecond)
    {
        if (bytesPerSecond > 1024 * 1024) // MB/s
        {
            return $"{bytesPerSecond / (1024 * 1024):F2} MB/s";
        }
        if (bytesPerSecond > 1024) // KB/s
        {
            return $"{bytesPerSecond / 1024:F1} KB/s";
        }
        return $"{bytesPerSecond:F0} B/s"; // B/s
    }

    }
}

