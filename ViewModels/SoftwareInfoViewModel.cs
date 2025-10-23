// 檔案: ViewModels/SoftwareInfoViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using collect_all.Models;
using collect_all.Services;

namespace collect_all.ViewModels
{
    public class SoftwareInfoViewModel : ViewModelBase
    {
        private readonly SoftwareCollectionService _softwareService;
        
        private ObservableCollection<Software> _installedSoftware;
        public ObservableCollection<Software> InstalledSoftware
        {
            get => _installedSoftware;
            set { _installedSoftware = value; OnPropertyChanged(); }
        }

        private string _softwareCount;
        public string SoftwareCount
        {
            get => _softwareCount;
            set { _softwareCount = value; OnPropertyChanged(); }
        }

        public SoftwareInfoViewModel()
        {
            _softwareService = new SoftwareCollectionService();
            _installedSoftware = new ObservableCollection<Software>();
            _softwareCount = "正在載入軟體資訊...";
            
            LoadSoftwareInfo();
        }

        private void LoadSoftwareInfo()
        {
            Task.Run(() =>
            {
                try
                {
                    LogService.Log("[SoftwareInfoViewModel] 開始載入軟體清單用於顯示");
                    List<Software> collectedSoftware = _softwareService.GetSoftwareFromRegistry();

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        InstalledSoftware.Clear();
                        
                        // 重新編號
                        for (int i = 0; i < collectedSoftware.Count; i++)
                        {
                            collectedSoftware[i].Number = i + 1;
                            InstalledSoftware.Add(collectedSoftware[i]);
                        }
                        
                        SoftwareCount = $"共找到 {InstalledSoftware.Count} 個已安裝的軟體";
                        LogService.Log($"[SoftwareInfoViewModel] 軟體清單載入完成，共 {InstalledSoftware.Count} 個");
                    });
                }
                catch (Exception ex)
                {
                    LogService.Log($"[SoftwareInfoViewModel] ✗ 錯誤: {ex.Message}");
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        SoftwareCount = $"載入錯誤: {ex.Message}";
                        System.Windows.MessageBox.Show($"載入軟體資訊時發生錯誤：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }
    }
}