// 檔案: Services/HardwareInfoService.cs
using System;
using System.Threading.Tasks;
using collect_all.Models;
using Microsoft.EntityFrameworkCore;

namespace collect_all.Services
{
    /// <summary>
    /// 硬體資訊服務 - 處理硬體資訊的上傳與更新
    /// </summary>
    public class HardwareInfoService
    {
        /// <summary>
        /// 上傳或更新硬體資訊（使用已收集的資料，避免重複查詢）
        /// </summary>
        public async Task UploadOrUpdateHardwareInfoAsync(string deviceNo, HardwareInfo currentInfo)
        {
            if (string.IsNullOrEmpty(deviceNo))
            {
                LogService.Log("[HardwareInfoService] 裝置編號為空，無法上傳硬體資訊");
                return;
            }

            if (currentInfo == null)
            {
                LogService.Log("[HardwareInfoService] 硬體資訊為空，無法上傳");
                return;
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    LogService.Log("[HardwareInfoService] 資料庫連線成功");

                    // 記錄收集到的硬體資訊
                    LogService.Log($"[HardwareInfoService] 硬體資訊：");
                    LogService.Log($"  - 處理器: {currentInfo.Processor}");
                    LogService.Log($"  - 主機板: {currentInfo.Motherboard}");
                    LogService.Log($"  - 記憶體總量: {currentInfo.MemoryTotalGB} GB");
                    LogService.Log($"  - 記憶體可用: {currentInfo.MemoryAvailableGB} GB");
                    LogService.Log($"  - IP 位址: {currentInfo.IPAddress}");

                    // 查詢資料庫中是否已有該裝置的記錄
                    var existingInfo = await db.HardwareInfos
                        .FirstOrDefaultAsync(h => h.DeviceNo == deviceNo);

                    if (existingInfo == null)
                    {
                        // 新增記錄
                        currentInfo.DeviceNo = deviceNo;
                        currentInfo.CreateDate = DateTime.Now;
                        currentInfo.UpdateDate = DateTime.Now;

                        await db.HardwareInfos.AddAsync(currentInfo);
                        await db.SaveChangesAsync();

                        LogService.Log($"[HardwareInfoService] ? 新增硬體資訊記錄（裝置編號: {deviceNo}）");
                    }
                    else
                    {
                        // 比對是否有變動
                        bool hasChanged = HasHardwareChanged(existingInfo, currentInfo);

                        if (hasChanged)
                        {
                            // 更新記錄（保留 CreateDate）
                            existingInfo.Processor = currentInfo.Processor;
                            existingInfo.Motherboard = currentInfo.Motherboard;
                            existingInfo.MemoryTotalGB = currentInfo.MemoryTotalGB;
                            existingInfo.MemoryAvailableGB = currentInfo.MemoryAvailableGB;
                            existingInfo.IPAddress = currentInfo.IPAddress;
                            existingInfo.UpdateDate = DateTime.Now;

                            await db.SaveChangesAsync();

                            LogService.Log($"[HardwareInfoService] ? 硬體資訊已變動，已更新記錄（裝置編號: {deviceNo}）");
                            LogService.Log($"[HardwareInfoService] 變動內容：");
                            LogComparisonDetails(existingInfo, currentInfo);
                        }
                        else
                        {
                            LogService.Log($"[HardwareInfoService] ? 硬體資訊無變動，不需要更新（裝置編號: {deviceNo}）");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"[HardwareInfoService] ? 上傳硬體資訊時發生錯誤：{ex.GetType().Name}");
                LogService.Log($"[HardwareInfoService] 錯誤訊息：{ex.Message}");
                if (ex.InnerException != null)
                {
                    LogService.Log($"[HardwareInfoService] 內部錯誤：{ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// 比對硬體資訊是否有變動
        /// </summary>
        private bool HasHardwareChanged(HardwareInfo existing, HardwareInfo current)
        {
            return existing.Processor != current.Processor ||
                   existing.Motherboard != current.Motherboard ||
                   existing.MemoryTotalGB != current.MemoryTotalGB ||
                   existing.IPAddress != current.IPAddress;
            // 注意：MemoryAvailableGB 會一直變動，所以不比對
        }

        /// <summary>
        /// 記錄比對的詳細資訊
        /// </summary>
        private void LogComparisonDetails(HardwareInfo existing, HardwareInfo current)
        {
            if (existing.Processor != current.Processor)
                LogService.Log($"  - 處理器: {existing.Processor} → {current.Processor}");

            if (existing.Motherboard != current.Motherboard)
                LogService.Log($"  - 主機板: {existing.Motherboard} → {current.Motherboard}");

            if (existing.MemoryTotalGB != current.MemoryTotalGB)
                LogService.Log($"  - 記憶體總量: {existing.MemoryTotalGB} GB → {current.MemoryTotalGB} GB");

            if (existing.IPAddress != current.IPAddress)
                LogService.Log($"  - IP 位址: {existing.IPAddress} → {current.IPAddress}");
        }
    }
}
