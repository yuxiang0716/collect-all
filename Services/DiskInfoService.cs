// 檔案: Services/DiskInfoService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using collect_all.Models;
using Microsoft.EntityFrameworkCore;

namespace collect_all.Services
{
    /// <summary>
    /// 磁碟資訊服務 - 處理磁碟資訊上傳與更新
    /// </summary>
    public class DiskInfoService
    {
        /// <summary>
        /// 收集所有磁碟槽資訊（用於上傳）
        /// </summary>
        public List<DiskInfo> CollectDiskInfos(string deviceNo)
        {
            var diskInfoList = new List<DiskInfo>();
            
            try
            {
                LogService.Log("[DiskInfoService] 開始收集磁碟槽資訊");
                
                // 取得所有準備好的磁碟機
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
                
                LogService.Log($"[DiskInfoService] 找到 {drives.Count} 個磁碟槽");
                
                foreach (var drive in drives)
                {
                    try
                    {
                        // 取得該磁碟槽所在的實體硬碟名稱
                        string physicalDiskName = GetPhysicalDiskName(drive.Name);
                        
                        var diskInfo = new DiskInfo
                        {
                            DeviceNo = deviceNo,
                            SlotName = drive.Name,  // 例如: C:\
                            TotalCapacityGB = (float)(drive.TotalSize / 1024.0 / 1024 / 1024),
                            AvailableCapacityGB = (float)(drive.AvailableFreeSpace / 1024.0 / 1024 / 1024),
                            DeviceInfoDeviceNo = physicalDiskName  // 暫時存放該槽所在的硬碟名稱
                        };
                        
                        diskInfoList.Add(diskInfo);
                        
                        LogService.Log($"[DiskInfoService] 收集磁碟槽: {drive.Name}, 總容量: {diskInfo.TotalCapacityGB:F2} GB, 可用: {diskInfo.AvailableCapacityGB:F2} GB, 硬碟: {physicalDiskName}");
                    }
                    catch (Exception ex)
                    {
                        LogService.Log($"[DiskInfoService] 收集磁碟槽 {drive.Name} 時發生錯誤: {ex.Message}");
                    }
                }
                
                LogService.Log($"[DiskInfoService] ? 磁碟槽資訊收集完成，共 {diskInfoList.Count} 個");
            }
            catch (Exception ex)
            {
                LogService.Log($"[DiskInfoService] ? 收集磁碟槽資訊時發生錯誤：{ex.Message}");
            }
            
            return diskInfoList;
        }
        
        /// <summary>
        /// 取得磁碟槽所在的實體硬碟名稱（使用 WMI 查詢）
        /// </summary>
        private string GetPhysicalDiskName(string driveLetter)
        {
            try
            {
                // 移除 :\ 只保留磁碟機代號 (例如 C:\ -> C)
                string driveLetterOnly = driveLetter.TrimEnd('\\', ':');
                
                // 使用 WMI 查詢該磁碟槽對應的實體硬碟
                using (var partitionSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetterOnly}:'}} WHERE AssocClass=Win32_LogicalDiskToPartition"))
                {
                    foreach (ManagementObject partition in partitionSearcher.Get())
                    {
                        using (var diskSearcher = new ManagementObjectSearcher(
                            $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
                        {
                            foreach (ManagementObject disk in diskSearcher.Get())
                            {
                                string model = disk["Model"]?.ToString() ?? "Unknown";
                                return model;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"[DiskInfoService] 無法取得 {driveLetter} 的實體硬碟名稱: {ex.Message}");
            }
            
            return "Unknown";
        }
        
        /// <summary>
        /// 上傳或更新磁碟資訊
        /// 邏輯：
        /// 1. 不重複上傳（如果資料相同則跳過）
        /// 2. 不刪除舊資料
        /// 3. 只刪除「資料庫有但電腦已不存在」的磁碟槽
        /// </summary>
        public async Task UploadOrUpdateDiskInfosAsync(string deviceNo, List<DiskInfo> currentDiskInfos)
        {
            if (string.IsNullOrEmpty(deviceNo))
            {
                LogService.Log("[DiskInfoService] 裝置編號為空，無法上傳磁碟資訊");
                return;
            }
            
            if (currentDiskInfos == null || currentDiskInfos.Count == 0)
            {
                LogService.Log("[DiskInfoService] 磁碟資訊為空，無法上傳");
                return;
            }
            
            try
            {
                using (var db = new AppDbContext())
                {
                    LogService.Log("[DiskInfoService] 資料庫連線成功");
                    
                    // 取得資料庫中該裝置的所有磁碟槽記錄
                    var existingDisks = await db.DiskInfos
                        .Where(d => d.DeviceNo == deviceNo)
                        .ToListAsync();
                    
                    LogService.Log($"[DiskInfoService] 資料庫中現有 {existingDisks.Count} 筆磁碟槽記錄");
                    
                    // 取得目前電腦存在的磁碟槽名稱列表
                    var currentSlotNames = currentDiskInfos.Select(d => d.SlotName).ToHashSet();
                    
                    // 1. 刪除「資料庫有但電腦已不存在」的磁碟槽
                    var disksToDelete = existingDisks
                        .Where(d => !currentSlotNames.Contains(d.SlotName))
                        .ToList();
                    
                    if (disksToDelete.Count > 0)
                    {
                        db.DiskInfos.RemoveRange(disksToDelete);
                        LogService.Log($"[DiskInfoService] ? 刪除 {disksToDelete.Count} 個已不存在的磁碟槽：{string.Join(", ", disksToDelete.Select(d => d.SlotName))}");
                    }
                    
                    // 2. 更新或新增磁碟槽資訊
                    int updatedCount = 0;
                    int addedCount = 0;
                    int unchangedCount = 0;
                    
                    foreach (var currentDisk in currentDiskInfos)
                    {
                        var existingDisk = existingDisks.FirstOrDefault(d => d.SlotName == currentDisk.SlotName);
                        
                        if (existingDisk == null)
                        {
                            // 新增磁碟槽
                            await db.DiskInfos.AddAsync(currentDisk);
                            addedCount++;
                            LogService.Log($"[DiskInfoService] + 新增磁碟槽: {currentDisk.SlotName}, {currentDisk.TotalCapacityGB:F2} GB, 硬碟: {currentDisk.DeviceInfoDeviceNo}");
                        }
                        else
                        {
                            // 檢查是否有變動
                            if (HasDiskInfoChanged(existingDisk, currentDisk))
                            {
                                // 更新資訊
                                existingDisk.TotalCapacityGB = currentDisk.TotalCapacityGB;
                                existingDisk.AvailableCapacityGB = currentDisk.AvailableCapacityGB;
                                existingDisk.DeviceInfoDeviceNo = currentDisk.DeviceInfoDeviceNo;
                                updatedCount++;
                                LogService.Log($"[DiskInfoService] ? 更新磁碟槽: {currentDisk.SlotName}, 總容量: {currentDisk.TotalCapacityGB:F2} GB, 可用: {currentDisk.AvailableCapacityGB:F2} GB");
                            }
                            else
                            {
                                unchangedCount++;
                            }
                        }
                    }
                    
                    // 儲存變更
                    int savedCount = await db.SaveChangesAsync();
                    
                    LogService.Log($"[DiskInfoService] ? 磁碟資訊上傳完成 (裝置: {deviceNo})");
                    LogService.Log($"[DiskInfoService]   - 新增: {addedCount} 個");
                    LogService.Log($"[DiskInfoService]   - 更新: {updatedCount} 個");
                    LogService.Log($"[DiskInfoService]   - 不變: {unchangedCount} 個");
                    LogService.Log($"[DiskInfoService]   - 刪除: {disksToDelete.Count} 個");
                    LogService.Log($"[DiskInfoService]   - 總共儲存: {savedCount} 筆變更");
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"[DiskInfoService] ? 上傳磁碟資訊時發生錯誤：{ex.GetType().Name}");
                LogService.Log($"[DiskInfoService] 錯誤訊息：{ex.Message}");
                if (ex.InnerException != null)
                {
                    LogService.Log($"[DiskInfoService] 內部錯誤：{ex.InnerException.Message}");
                }
            }
        }
        
        /// <summary>
        /// 檢查磁碟資訊是否有變動
        /// </summary>
        private bool HasDiskInfoChanged(DiskInfo existing, DiskInfo current)
        {
            // 比較總容量（容許 0.1 GB 誤差）
            bool capacityChanged = Math.Abs(existing.TotalCapacityGB - current.TotalCapacityGB) > 0.1f;
            
            // 比較可用空間（容許 1 GB 誤差，因為可用空間經常變動）
            bool availableChanged = Math.Abs(existing.AvailableCapacityGB - current.AvailableCapacityGB) > 1.0f;
            
            // 比較硬碟名稱
            bool deviceChanged = existing.DeviceInfoDeviceNo != current.DeviceInfoDeviceNo;
            
            return capacityChanged || availableChanged || deviceChanged;
        }
    }
}
