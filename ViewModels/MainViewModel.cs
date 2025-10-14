// 檔案: ViewModels/MainViewModel.cs
// 職責: 擔任 MainWindow 的大腦，處理所有 UI 邏輯、資料綁定和命令。

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using collect_all.Commands;
using collect_all.Models;
using collect_all.Services;
using collect_all.Views; // 引用 Views 資料夾

namespace collect_all.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly SystemInfoService _infoService;
        private bool _isUpdatingSensors = false;

        // --- 綁定到 UI 的屬性 ---
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
                    // 當 CheckBox 狀態改變時，呼叫服務更新設定
                    if (value) StartupManager.SetStartup();
                    else StartupManager.RemoveStartup();
                }
            }
        }

        // --- 綁定到 UI 的命令 ---
        public ICommand RefreshCommand { get; }
        public ICommand ShowSoftwareInfoCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand RegisterCommand { get; }

        // --- 建構函式 ---
        public MainViewModel()
        {
            // 實例化服務
            _infoService = new SystemInfoService();

            // 初始化資料集合
            SystemInfoItems = new ObservableCollection<BasicInfoData>();
            HardwareItems = new ObservableCollection<BasicInfoData>();
            StorageVgaItems = new ObservableCollection<BasicInfoData>();
            TemperatureItems = new ObservableCollection<BasicInfoData>();
            SmartItems = new ObservableCollection<BasicInfoData>();
            
            // 從服務讀取初始狀態
            _isStartupSet = StartupManager.IsStartupSet();

            // 設定命令 (將 UI 的點擊事件，連結到 ViewModel 的方法)
            RefreshCommand = new RelayCommand(async _ => await UpdateSensorsAsync());
            ShowSoftwareInfoCommand = new RelayCommand(_ => new SoftwareInfoWindow().Show());
            LoginCommand = new RelayCommand(_ => new LoginWindow().ShowDialog());
            RegisterCommand = new RelayCommand(_ => new RegisterWindow().ShowDialog());
            
            // 載入所有初始資訊
            LoadStaticInfo();
            _ = UpdateSensorsAsync(); // 啟動時非同步更新一次感應器
        }

        // --- 私有方法 ---
        private void LoadStaticInfo()
        {
            // 清空列表
            SystemInfoItems.Clear();
            HardwareItems.Clear();
            StorageVgaItems.Clear();

            // 從服務獲取靜態資訊並填入集合
            SystemInfoItems.Add(new BasicInfoData("電腦名稱", Environment.MachineName));
            SystemInfoItems.Add(new BasicInfoData("作業系統", Environment.OSVersion.ToString()));
            SystemInfoItems.Add(new BasicInfoData("Windows 版本", _infoService.GetWindowsVersion()));
            SystemInfoItems.Add(new BasicInfoData("網路 IP", _infoService.GetLocalIPv4()));
            SystemInfoItems.Add(new BasicInfoData("MAC 位址", _infoService.GetMacAddress()));

            HardwareItems.Add(new BasicInfoData("CPU 型號", _infoService.GetCpuName()));
            HardwareItems.Add(new BasicInfoData("CPU 核心數", _infoService.GetCpuCoreCount().ToString()));
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
        }

        private async Task UpdateSensorsAsync()
        {
            if (_isUpdatingSensors) return; // 防止重複執行
            _isUpdatingSensors = true;

            // 在背景執行緒中獲取感應器資料，避免 UI 凍結
            var temps = await Task.Run(() => _infoService.GetTemperatures());
            var smarts = await Task.Run(() => _infoService.GetSmartHealth());
            
            // 回到 UI 執行緒更新集合
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                TemperatureItems.Clear();
                foreach(var item in temps) TemperatureItems.Add(item);

                SmartItems.Clear();
                foreach(var item in smarts) SmartItems.Add(item);
            });

            _isUpdatingSensors = false;
        }

        public override void Dispose()
        {
            _infoService.Dispose();
            base.Dispose(); // 加上這一行，呼叫基底類別的 Dispose 是一個好習慣
        }
    }
}