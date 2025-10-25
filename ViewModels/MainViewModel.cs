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
        private readonly SoftwareCollectionService _softwareService;
        private readonly PowerLogService _powerLogService;
        private readonly HardwareInfoService _hardwareInfoService;
        private readonly GraphicsCardInfoService _graphicsCardInfoService;
        private readonly DiskInfoService _diskInfoService;  // 新增 DiskInfoService
        private readonly SettingsService _settingsService;  // 新增 SettingsService
        private bool _isUpdatingSensors = false;
        private bool _isLoggingIn = false;  // 防止重複登入

        private List<PerformanceCounter> _downloadCounters;
        private List<PerformanceCounter> _uploadCounters;
        private List<string> _counterInstanceNames;
        private DispatcherTimer? _networkTimer;

        private CancellationTokenSource _cts = new CancellationTokenSource();

        private int _networkUpdateIntervalSeconds = 1;   // 網路預設 1 秒
        private int _sensorUpdateIntervalSeconds = 30;  // 感應器預設 30 秒（改為 30 秒）
        private string _userIdentifierText = "正在初始化..."; // 初始文字
        
        // 暫存軟體清單
        private List<Software>? _collectedSoftware = null;
        private bool _softwareUploaded = false;  // 追蹤軟體是否已上傳
        
        // 暫存開關機記錄
        private List<PowerLog>? _collectedPowerLogs = null;
        private bool _powerLogsUploaded = false;  // 追蹤開關機記錄是否已上傳

        // 硬體資訊上傳標記
        private bool _hardwareInfoUploaded = false;
        
        // 暫存硬體資訊（避免重複收集）
        private HardwareInfo? _collectedHardwareInfo = null;
        
        // 顯卡資訊上傳標記和暫存
        private bool _graphicsCardsUploaded = false;
        private List<string>? _collectedGraphicsCards = null;
        
        // 磁碟資訊上傳標記和暫存
        private bool _diskInfosUploaded = false;
        private List<DiskInfo>? _collectedDiskInfos = null;

        private string _loginButtonText = "登入";
        public string LoginButtonText
        {
            get => _loginButtonText;
            set { _loginButtonText = value; OnPropertyChanged(); }
        }

        private bool _isLoginButtonEnabled = true;
        public bool IsLoginButtonEnabled
        {
            get => _isLoginButtonEnabled;
            set { _isLoginButtonEnabled = value; OnPropertyChanged(); }
        }

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
        }

        // --- 新增：綁定到設定頁面的感應器更新頻率 ---
        public string SensorUpdateIntervalSeconds
        {
            get => _sensorUpdateIntervalSeconds.ToString();
        }




        public ICommand RefreshCommand { get; }
        public ICommand ShowSoftwareInfoCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand ShowBootHistoryCommand { get; }
        public ICommand ShowLogViewerCommand { get; }

        public MainViewModel()
        {
            _infoService = new SystemInfoService();
            _macLoginService = new SystemMacLoginService();
            _softwareService = new SoftwareCollectionService();
            _powerLogService = new PowerLogService();
            _hardwareInfoService = new HardwareInfoService();
            _graphicsCardInfoService = new GraphicsCardInfoService();
            _diskInfoService = new DiskInfoService();  // 初始化 DiskInfoService
            _settingsService = new SettingsService();  // 初始化 SettingsService

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
            ShowLogViewerCommand = new RelayCommand(_ => new LogViewerWindow().Show());

            AuthenticationService.Instance.AuthenticationStateChanged += OnAuthenticationStateChanged;

            // 監聽系統關機事件
            Microsoft.Win32.SystemEvents.SessionEnding += OnSystemShutdown;

            // 在背景執行啟動流程
            Task.Run(async () => await StartupFlowAsync());
        }
        




        private async Task StartupFlowAsync()
        {
            // 1a. 在背景先收集軟體清單（不阻塞登入）
            Task.Run(() =>
            {
                try
                {
                    _collectedSoftware = _softwareService.GetSoftwareFromRegistry();
                    LogService.Log($"[MainViewModel] 軟體清單收集完成，共 {_collectedSoftware.Count} 個軟體");
                }
                catch (Exception ex)
                {
                    LogService.Log($"[MainViewModel] 收集軟體清單時發生錯誤：{ex.Message}");
                }
            });

            // 1b. 在背景收集開關機記錄（不阻塞登入）
            Task.Run(() =>
            {
                try
                {
                    _collectedPowerLogs = _powerLogService.GetPowerLogs(30);  // 收集最近 30 天
                    LogService.Log($"[MainViewModel] 開關機記錄收集完成，共 {_collectedPowerLogs.Count} 筆");
                }
                catch (Exception ex)
                {
                    LogService.Log($"[MainViewModel] 收集開關機記錄時發生錯誤：{ex.Message}");
                }
            });

            // 2. 執行登入流程
            await RequestMacLoginAsync(onStartup: true);

            // 2.5. 登入後載入設定（新增）
            await LoadSettingsAsync();

            // 3. 登入流程結束後，載入系統資訊並顯示資料
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LoadStaticInfoAndSendToDb();
                // 登入時立即執行一次感應器更新（溫度、SMART、使用率）
                _ = UpdateSensorsAsync();
            });
            
            // 4. 啟動週期性更新（依據設定的間隔）
            _ = StartPeriodicUpdatesAsync(_cts.Token);
        }

        /// <summary>
        /// 載入設定（根據登入狀態和公司名稱）
        /// </summary>
        private async Task LoadSettingsAsync()
        {
            try
            {
                var currentUser = AuthenticationService.Instance.CurrentUser;
                string companyName = currentUser?.CompanyName ?? string.Empty;

                if (string.IsNullOrEmpty(companyName))
                {
                    LogService.Log("[MainViewModel] 使用者未登入，使用預設設定");
                    companyName = "admin";  // 未登入時嘗試使用 admin 設定
                }

                // 呼叫 SettingsService 取得設定
                var (networkInterval, hardwareInterval) = await _settingsService.GetSettingsAsync(companyName);

                // 更新間隔設定
                _networkUpdateIntervalSeconds = networkInterval;
                _sensorUpdateIntervalSeconds = hardwareInterval;

                LogService.Log($"[MainViewModel] 設定已載入 - 網路更新間隔: {networkInterval} 秒, 硬體更新間隔: {hardwareInterval} 秒");

                // 通知 UI 更新（如果有綁定）
                OnPropertyChanged(nameof(NetworkUpdateIntervalSeconds));
                OnPropertyChanged(nameof(SensorUpdateIntervalSeconds));

                // 重新啟動計時器和週期更新（使用新的間隔）
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 重新設定網路計時器
                    if (_networkTimer != null)
                    {
                        _networkTimer.Stop();
                        _networkTimer.Interval = TimeSpan.FromSeconds(_networkUpdateIntervalSeconds);
                        _networkTimer.Start();
                        LogService.Log($"[MainViewModel] 網路計時器已更新為 {_networkUpdateIntervalSeconds} 秒");
                    }
                });

                // 重新啟動感應器週期更新
                RestartPeriodicSensorUpdates();
                LogService.Log($"[MainViewModel] 感應器週期更新已重啟，間隔 {_sensorUpdateIntervalSeconds} 秒");
            }
            catch (Exception ex)
            {
                LogService.Log($"[MainViewModel] 載入設定時發生錯誤：{ex.Message}");
                // 發生錯誤時使用預設值
                _networkUpdateIntervalSeconds = 1;
                _sensorUpdateIntervalSeconds = 30;
            }
        }

        private async Task RequestMacLoginAsync(bool onStartup = false)
        {
            // 檢查是否已經登入
            if (AuthenticationService.Instance.CurrentUser != null)
            {
                LogService.Log("[MainViewModel] 已經登入，無需再次登入");
                System.Windows.MessageBox.Show("您已經登入了！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // 防止重複登入
            if (_isLoggingIn)
            {
                LogService.Log("[MainViewModel] 正在登入中，跳過重複的登入請求");
                return;
            }

            _isLoggingIn = true;
            LoginButtonText = "登入中...";  // 更新按鈕文字
            IsLoginButtonEnabled = false;  // 禁用按鈕
            
            try
            {
                string mac = _macLoginService.GetPrimaryMac();
                if (string.IsNullOrEmpty(mac))
                {
                    var user = UIAuthService.ShowLoginDialog();
                    await UpdateUserIdentifierText();
                    
                    // 登入後嘗試上傳軟體資訊
                    await TrySendSoftwareInfoAsync();
                    await TrySendPowerLogsAsync();  // 同時上傳開關機記錄
                    await TrySendHardwareInfoAsync();  // 同時上傳硬體資訊
                    await TrySendGraphicsCardsAsync();  // 同時上傳顯卡資訊
                    await TrySendDiskInfosAsync();  // 同時上傳磁碟資訊

                    // 登入成功後隱藏登入按鈕
                    if (AuthenticationService.Instance.CurrentUser != null)
                    {
                        LoginButtonText = "已登入";
                        IsLoginButtonEnabled = false;
                    }
                    return;
                }

                var (found, macRecord) = await _macLoginService.CheckMacInTableAsync(mac);
                if (found && macRecord != null)
                {
                    var user = UIAuthService.ShowLoginDialog(macRecord.User, isUsernameReadOnly: true);
                    await UpdateUserIdentifierText();
                    
                    // 登入後嘗試上傳軟體資訊
                    await TrySendSoftwareInfoAsync();
                    await TrySendPowerLogsAsync();  // 同時上傳開關機記錄
                    await TrySendHardwareInfoAsync();  // 同時上傳硬體資訊
                    await TrySendGraphicsCardsAsync();  // 同時上傳顯卡資訊
                    await TrySendDiskInfosAsync();  // 同時上傳磁碟資訊

                    // 登入成功後隱藏登入按鈕
                    if (AuthenticationService.Instance.CurrentUser != null)
                    {
                        LoginButtonText = "已登入";
                        IsLoginButtonEnabled = false;
                    }
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
                    
                    // 登入後嘗試上傳軟體資訊
                    await TrySendSoftwareInfoAsync();
                    await TrySendPowerLogsAsync();  // 同時上傳開關機記錄
                    await TrySendHardwareInfoAsync();  // 同時上傳硬體資訊
                    await TrySendGraphicsCardsAsync();  // 同時上傳顯卡資訊
                    await TrySendDiskInfosAsync();  // 同時上傳磁碟資訊

                    // 登入成功後隱藏登入按鈕
                    if (AuthenticationService.Instance.CurrentUser != null)
                    {
                        LoginButtonText = "已登入";
                        IsLoginButtonEnabled = false;
                    }
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
            finally
            {
                _isLoggingIn = false;  // 無論成功或失敗都要解除鎖定
                
                // 如果未登入，恢復按鈕狀態
                if (AuthenticationService.Instance.CurrentUser == null)
                {
                    LoginButtonText = "登入";
                    IsLoginButtonEnabled = true;
                }
            }
        }

        private async Task TrySendSoftwareInfoAsync()
        {
            // 檢查是否已經上傳過
            if (_softwareUploaded)
            {
                LogService.Log("[MainViewModel] 軟體資訊已經上傳過，跳過重複上傳");
                return;
            }
            
            // 檢查使用者是否已登入
            var currentUser = AuthenticationService.Instance.CurrentUser;
            if (currentUser == null)
            {
                LogService.Log("[MainViewModel] 使用者未登入，無法上傳軟體資訊");
                return;
            }

            // 檢查軟體清單是否已收集
            if (_collectedSoftware == null)
            {
                LogService.Log("[MainViewModel] 軟體清單尚未收集完成，等待中...");
                
                // 等待軟體收集完成（最多等待 30 秒）
                int waitCount = 0;
                while (_collectedSoftware == null && waitCount < 60)
                {
                    await Task.Delay(500);
                    waitCount++;
                }
                
                if (_collectedSoftware == null)
                {
                    LogService.Log("[MainViewModel] ✗ 等待軟體清單收集逾時");
                    return;
                }
            }

            // 上傳軟體資訊
            LogService.Log($"[MainViewModel] 準備上傳 {_collectedSoftware.Count} 筆軟體資訊");
            await DataSendService.SendSoftwareAsync(_collectedSoftware);
            
            // 標記為已上傳
            _softwareUploaded = true;
            LogService.Log("[MainViewModel] ✓ 軟體資訊上傳流程完成，已標記為已上傳");
        }

        private async Task TrySendPowerLogsAsync()
        {
            // 檢查是否已經上傳過（每次啟動只上傳一次）
            if (_powerLogsUploaded)
            {
                LogService.Log("[MainViewModel] 開關機記錄已經上傳過，跳過重複上傳");
                return;
            }
            
            // 檢查使用者是否已登入
            var currentUser = AuthenticationService.Instance.CurrentUser;
            if (currentUser == null)
            {
                LogService.Log("[MainViewModel] 使用者未登入，無法上傳開關機記錄");
                return;
            }

            // 取得裝置編號
            string mac = _macLoginService.GetPrimaryMac();
            var (found, macRecord) = await _macLoginService.CheckMacInTableAsync(mac);

            if (!found || macRecord == null || string.IsNullOrEmpty(macRecord.DeviceId))
            {
                LogService.Log("[MainViewModel] 無法取得裝置編號，無法上傳開關機記錄");
                return;
            }

            string deviceNo = macRecord.DeviceId;

            // 檢查開關機記錄是否已收集
            if (_collectedPowerLogs == null)
            {
                LogService.Log("[MainViewModel] 開關機記錄尚未收集完成，等待中...");
                
                // 等待收集完成（最多等待 10 秒）
                int waitCount = 0;
                while (_collectedPowerLogs == null && waitCount < 20)
                {
                    await Task.Delay(500);
                    waitCount++;
                }
                
                if (_collectedPowerLogs == null)
                {
                    LogService.Log("[MainViewModel] ✗ 等待開關機記錄收集逾時");
                    return;
                }
            }

            // 上傳開關機記錄（服務會自動過濾已存在的記錄）
            LogService.Log($"[MainViewModel] 準備上傳 {_collectedPowerLogs.Count} 筆開關機記錄（自動過濾重複）");
            await _powerLogService.UploadPowerLogsAsync(deviceNo, _collectedPowerLogs);
            
            // 標記為已上傳
            _powerLogsUploaded = true;
            LogService.Log("[MainViewModel] ✓ 開關機記錄上傳流程完成，已標記為已上傳");
        }

        private async Task TrySendHardwareInfoAsync()
        {
            // 檢查是否已經上傳過（每次啟動只上傳一次）
            if (_hardwareInfoUploaded)
            {
                LogService.Log("[MainViewModel] 硬體資訊已經上傳過，跳過重複上傳");
                return;
            }

            // 檢查使用者是否已登入
            var currentUser = AuthenticationService.Instance.CurrentUser;
            if (currentUser == null)
            {
                LogService.Log("[MainViewModel] 使用者未登入，無法上傳硬體資訊");
                return;
            }

            // 檢查硬體資訊是否已收集
            if (_collectedHardwareInfo == null)
            {
                LogService.Log("[MainViewModel] 硬體資訊尚未收集，等待中...");
                
                // 等待收集完成（最多等待 5 秒）
                int waitCount = 0;
                while (_collectedHardwareInfo == null && waitCount < 10)
                {
                    await Task.Delay(500);
                    waitCount++;
                }
                
                if (_collectedHardwareInfo == null)
                {
                    LogService.Log("[MainViewModel] ✗ 等待硬體資訊收集逾時");
                    return;
                }
            }

            // 取得裝置編號
            string mac = _macLoginService.GetPrimaryMac();
            var (found, macRecord) = await _macLoginService.CheckMacInTableAsync(mac);

            if (!found || macRecord == null || string.IsNullOrEmpty(macRecord.DeviceId))
            {
                LogService.Log("[MainViewModel] 無法取得裝置編號，無法上傳硬體資訊");
                return;
            }

            string deviceNo = macRecord.DeviceId;
            
            // 設定裝置編號
            _collectedHardwareInfo.DeviceNo = deviceNo;

            // 上傳或更新硬體資訊（使用暫存的資料）
            LogService.Log($"[MainViewModel] 準備上傳/更新硬體資訊（裝置編號: {deviceNo}）");
            await _hardwareInfoService.UploadOrUpdateHardwareInfoAsync(deviceNo, _collectedHardwareInfo);

            // 標記為已上傳
            _hardwareInfoUploaded = true;
            LogService.Log("[MainViewModel] ✓ 硬體資訊上傳流程完成，已標記為已上傳");
        }

        private async Task TrySendGraphicsCardsAsync()
        {
            // 檢查是否已經上傳過（每次啟動只上傳一次）
            if (_graphicsCardsUploaded)
            {
                LogService.Log("[MainViewModel] 顯卡資訊已經上傳過，跳過重複上傳");
                return;
            }

            // 檢查使用者是否已登入
            var currentUser = AuthenticationService.Instance.CurrentUser;
            if (currentUser == null)
            {
                LogService.Log("[MainViewModel] 使用者未登入，無法上傳顯卡資訊");
                return;
            }

            // 檢查顯卡資訊是否已收集
            if (_collectedGraphicsCards == null)
            {
                LogService.Log("[MainViewModel] 顯卡資訊尚未收集，等待中...");
                
                // 等待收集完成（最多等待 5 秒）
                int waitCount = 0;
                while (_collectedGraphicsCards == null && waitCount < 10)
                {
                    await Task.Delay(500);
                    waitCount++;
                }
                
                if (_collectedGraphicsCards == null)
                {
                    LogService.Log("[MainViewModel] ✗ 等待顯卡資訊收集逾時");
                    return;
                }
            }

            // 取得裝置編號
            string mac = _macLoginService.GetPrimaryMac();
            var (found, macRecord) = await _macLoginService.CheckMacInTableAsync(mac);

            if (!found || macRecord == null || string.IsNullOrEmpty(macRecord.DeviceId))
            {
                LogService.Log("[MainViewModel] 無法取得裝置編號，無法上傳顯卡資訊");
                return;
            }

            string deviceNo = macRecord.DeviceId;
            
            // 上傳顯卡資訊
            LogService.Log($"[MainViewModel] 準備上傳 {_collectedGraphicsCards.Count} 筆顯卡資訊");
            await _graphicsCardInfoService.UploadOrUpdateGraphicsCardsAsync(deviceNo, _collectedGraphicsCards);
            
            // 標記為已上傳
            _graphicsCardsUploaded = true;
            LogService.Log("[MainViewModel] ✓ 顯卡資訊上傳流程完成，已標記為已上傳");
        }

        private async Task TrySendDiskInfosAsync()
        {
            // 檢查是否已經上傳過（每次啟動只上傳一次）
            if (_diskInfosUploaded)
            {
                LogService.Log("[MainViewModel] 磁碟資訊已經上傳過，跳過重複上傳");
                return;
            }

            // 檢查使用者是否已登入
            var currentUser = AuthenticationService.Instance.CurrentUser;
            if (currentUser == null)
            {
                LogService.Log("[MainViewModel] 使用者未登入，無法上傳磁碟資訊");
                return;
            }

            // 取得裝置編號
            string mac = _macLoginService.GetPrimaryMac();
            var (found, macRecord) = await _macLoginService.CheckMacInTableAsync(mac);

            if (!found || macRecord == null || string.IsNullOrEmpty(macRecord.DeviceId))
            {
                LogService.Log("[MainViewModel] 無法取得裝置編號，無法上傳磁碟資訊");
                return;
            }

            string deviceNo = macRecord.DeviceId;
            
            // 收集磁碟資訊（使用 DiskInfoService）
            var diskInfos = _diskInfoService.CollectDiskInfos(deviceNo);
            
            if (diskInfos == null || diskInfos.Count == 0)
            {
                LogService.Log("[MainViewModel] ✗ 磁碟資訊收集失敗或無磁碟");
                return;
            }

            // 上傳磁碟資訊
            LogService.Log($"[MainViewModel] 準備上傳 {diskInfos.Count} 個磁碟槽資訊");
            await _diskInfoService.UploadOrUpdateDiskInfosAsync(deviceNo, diskInfos);
            
            // 標記為已上傳
            _diskInfosUploaded = true;
            LogService.Log("[MainViewModel] ✓ 磁碟資訊上傳流程完成，已標記為已上傳");
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

            // 收集硬體資訊（這些資料會被重複使用）
            string cpuName = _infoService.GetCpuName();
            string cpuCoreCount = _infoService.GetCpuCoreCount();
            string moboManufacturer = _infoService.GetMotherboardManufacturer();
            string moboModel = _infoService.GetMotherboardModel();
            string totalRam = _infoService.GetTotalRAM();
            string availableRam = _infoService.GetAvailableRAM();
            string ipAddress = _infoService.GetLocalIPv4();

            HardwareItems.Add(new BasicInfoData("CPU 型號", cpuName));
            HardwareItems.Add(new BasicInfoData("CPU 核心數", cpuCoreCount));
            HardwareItems.Add(new BasicInfoData("主機板製造商", moboManufacturer));
            HardwareItems.Add(new BasicInfoData("主機板型號", moboModel));
            HardwareItems.Add(new BasicInfoData("記憶體總容量 (GB)", totalRam));
            HardwareItems.Add(new BasicInfoData("記憶體剩餘容量 (GB)", availableRam));

            StorageVgaItems.Add(new BasicInfoData("獨立顯示卡 (GPU)", _infoService.GetGpuName(true)));
            StorageVgaItems.Add(new BasicInfoData("內建顯示卡", _infoService.GetGpuName(false)));
            StorageVgaItems.Add(new BasicInfoData("顯示卡 VRAM (GB)", _infoService.GetGpuVRAM()));
            var allDrives = _infoService.GetAllDrivesInfo();
            foreach (var d in allDrives)
                StorageVgaItems.Add(d);

            // 收集所有顯卡（重複使用現有的查詢邏輯，避免重複查詢）
            _collectedGraphicsCards = _infoService.GetAllGpuNames();
            LogService.Log("[MainViewModel] 顯卡資訊已收集並暫存，供資料庫上傳使用");

            // 暫存硬體資訊供上傳使用（避免重複收集）
            _collectedHardwareInfo = new HardwareInfo
            {
                DeviceNo = string.Empty,  // 稍後設定
                Processor = cpuName,
                Motherboard = GetMotherboardInfo(moboManufacturer, moboModel),
                MemoryTotalGB = ParseMemoryGB(totalRam),
                MemoryAvailableGB = ParseMemoryGB(availableRam),
                IPAddress = ipAddress
            };

            LogService.Log("[MainViewModel] 硬體資訊已收集並暫存，供 UI 顯示和資料庫上傳使用");

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

        /// <summary>
        /// 取得主機板資訊（製造商 + 型號）
        /// </summary>
        private string GetMotherboardInfo(string manufacturer, string model)
        {
            if (manufacturer == "不支援" && model == "不支援")
                return "不支援";

            if (manufacturer == "不支援")
                return model;

            if (model == "不支援")
                return manufacturer;

            return $"{manufacturer} {model}";
        }

        /// <summary>
        /// 解析記憶體 GB 字串為 float
        /// </summary>
        private float ParseMemoryGB(string memoryStr)
        {
            if (string.IsNullOrEmpty(memoryStr) || memoryStr == "不支援")
                return 0;

            try
            {
                memoryStr = memoryStr.Replace("GB", "").Trim();

                if (float.TryParse(memoryStr, out float value))
                {
                    return value;  // 保留小數點精度
                }

                return 0;
            }
            catch
            {
                return 0;
            }
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

        private async Task UpdateSensorsAsync()
        {
            if (_isUpdatingSensors) return;
            _isUpdatingSensors = true;

            // 平行抓取溫度、SMART 和使用率
            var tempsTask = Task.Run(() => _infoService.GetTemperatures());
            var smartsTask = Task.Run(() => _infoService.GetSmartHealth());
            var usagesTask = Task.Run(() => _infoService.GetUsage());

            await Task.WhenAll(tempsTask, smartsTask, usagesTask);

            var temps = await tempsTask;
            var smarts = await smartsTask;
            var usages = await usagesTask;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                UsageItems.Clear();
                foreach (var item in usages) UsageItems.Add(item);

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
                LogService.Log($"Periodic update error: {ex.Message}");
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
            _cts.Cancel();
            _cts.Dispose();

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
            Microsoft.Win32.SystemEvents.SessionEnding -= OnSystemShutdown;
            
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


        private void OnAuthenticationStateChanged(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(async () =>
            {
                await UpdateUserIdentifierText();
                
                // 登入狀態改變後，重新載入設定（新增）
                await LoadSettingsAsync();
                
                // 登入狀態改變後，重新觸發一次資料載入與傳送
                LoadStaticInfoAndSendToDb();
                
                // 如果是登入事件，立即抓取一次感應器資料
                if (AuthenticationService.Instance.CurrentUser != null)
                {
                    LogService.Log("[MainViewModel] 登入成功，立即執行一次感應器資料抓取");
                    await UpdateSensorsAsync();
                    
                    await TrySendSoftwareInfoAsync();
                    // 移除：await TrySendPowerLogsAsync(); // 開關機記錄只在 RequestMacLoginAsync 中上傳一次
                }
            });
        }

        // 系統關機時的處理
        private void OnSystemShutdown(object sender, Microsoft.Win32.SessionEndingEventArgs e)
        {
            LogService.Log($"[MainViewModel] 偵測到系統關機事件：{e.Reason}");

            try
            {
                // 檢查是否已登入
                if (AuthenticationService.Instance.CurrentUser == null)
                {
                    LogService.Log("[MainViewModel] 使用者未登入，跳過關機前上傳");
                    return;
                }

                // 取得裝置編號
                string mac = _macLoginService.GetPrimaryMac();
                var task = _macLoginService.CheckMacInTableAsync(mac);
                task.Wait(TimeSpan.FromSeconds(5));  // 最多等待 5 秒
                
                var (found, macRecord) = task.Result;
                
                if (!found || macRecord == null || string.IsNullOrEmpty(macRecord.DeviceId))
                {
                    LogService.Log("[MainViewModel] 無法取得裝置編號，跳過關機前上傳");
                    return;
                }

                string deviceNo = macRecord.DeviceId;

                // 快速收集並上傳最新的開關機記錄
                LogService.Log("[MainViewModel] 關機前快速上傳最新開關機記錄...");
                
                var powerLogs = _powerLogService.GetPowerLogs(7);  // 只收集最近 7 天（速度較快）
                
                if (powerLogs != null && powerLogs.Count > 0)
                {
                    var uploadTask = _powerLogService.UploadPowerLogsAsync(deviceNo, powerLogs);
                    uploadTask.Wait(TimeSpan.FromSeconds(8));  // 最多等待 8 秒
                    
                    LogService.Log("[MainViewModel] ✓ 關機前上傳完成");
                }
                else
                {
                    LogService.Log("[MainViewModel] 沒有新的開關機記錄需要上傳");
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"[MainViewModel] ✗ 關機前上傳失敗：{ex.Message}");
            }
        }

        // 測試用：手動觸發靜態資訊上傳
        public async void TriggerStaticInfoUpload()
        {
            LogService.Log("[MainViewModel] 手動觸發靜態資訊上傳");
            LoadStaticInfoAndSendToDb();
        }
    }
}

