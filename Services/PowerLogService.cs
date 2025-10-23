// 檔案: Services/PowerLogService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Threading.Tasks;
using collect_all.Models;
using Microsoft.EntityFrameworkCore;

namespace collect_all.Services
{
    /// <summary>
    /// 開關機記錄服務
    /// </summary>
    public class PowerLogService
    {
        /// <summary>
        /// 收集最近 N 天的開關機記錄
        /// </summary>
        public List<PowerLog> GetPowerLogs(int days = 30)
        {
            LogService.Log($"[PowerLogService] 開始收集最近 {days} 天的開關機記錄...");
            
            var powerLogs = new List<PowerLog>();
            DateTime startTime = DateTime.Now.AddDays(-days);

            string queryString =
                $"*[System[(" +
                "(Provider[@Name='Microsoft-Windows-Kernel-General'] and (EventID=12 or EventID=13))" +
                $") and TimeCreated[timediff(@SystemTime) <= {DateTime.Now.Subtract(startTime).TotalMilliseconds}]]]";

            EventLogQuery query = new EventLogQuery("System", PathType.LogName, queryString);

            try
            {
                EventLogReader reader = new EventLogReader(query);
                EventRecord? record;

                while ((record = reader.ReadEvent()) != null)
                {
                    using (record)
                    {
                        if (record.TimeCreated.HasValue)
                        {
                            string action = record.Id switch
                            {
                                12 => "Startup",   // 開機
                                13 => "Shutdown",  // 關機
                                _ => "Unknown"
                            };

                            powerLogs.Add(new PowerLog
                            {
                                Timestamp = record.TimeCreated.Value.ToLocalTime(),
                                Action = action,
                                DeviceNo = string.Empty  // 稍後會設定
                            });
                        }
                    }
                }

                LogService.Log($"[PowerLogService] 收集完成，共 {powerLogs.Count} 筆開關機記錄");
            }
            catch (EventLogException ex)
            {
                LogService.Log($"[PowerLogService] ? 讀取事件日誌時發生錯誤：{ex.Message}");
                LogService.Log("[PowerLogService] 提示：請確認程式是以系統管理員身分執行");
            }
            catch (Exception ex)
            {
                LogService.Log($"[PowerLogService] ? 收集開關機記錄時發生錯誤：{ex.Message}");
            }

            return powerLogs;
        }

        /// <summary>
        /// 上傳開關機記錄到資料庫（只上傳新記錄，優化版）
        /// </summary>
        public async Task UploadPowerLogsAsync(string deviceNo, List<PowerLog> powerLogs)
        {
            if (string.IsNullOrEmpty(deviceNo))
            {
                LogService.Log("[PowerLogService] 裝置編號為空，無法上傳開關機記錄");
                return;
            }

            if (powerLogs == null || powerLogs.Count == 0)
            {
                LogService.Log("[PowerLogService] 沒有開關機記錄可上傳");
                return;
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    LogService.Log("[PowerLogService] 資料庫連線成功");

                    // 設定所有記錄的 DeviceNo
                    foreach (var log in powerLogs)
                    {
                        log.DeviceNo = deviceNo;
                    }

                    // 優化：取得要上傳的記錄的時間範圍
                    var minTimestamp = powerLogs.Min(p => p.Timestamp);
                    var maxTimestamp = powerLogs.Max(p => p.Timestamp);

                    LogService.Log($"[PowerLogService] 準備上傳時間範圍：{minTimestamp:yyyy-MM-dd HH:mm:ss} 到 {maxTimestamp:yyyy-MM-dd HH:mm:ss}");

                    // 查詢該時間範圍內已存在的記錄（使用複合鍵：DeviceNo + Timestamp + Action）
                    var existingRecords = await db.PowerLogs
                        .Where(p => p.DeviceNo == deviceNo 
                                 && p.Timestamp >= minTimestamp 
                                 && p.Timestamp <= maxTimestamp)
                        .Select(p => new { p.Timestamp, p.Action })
                        .ToListAsync();

                    // 建立 HashSet 用於快速查找（使用 Timestamp + Action 複合鍵）
                    var existingKeys = new HashSet<string>(
                        existingRecords.Select(r => $"{r.Timestamp:yyyy-MM-dd HH:mm:ss}|{r.Action}")
                    );

                    LogService.Log($"[PowerLogService] 資料庫中該時間範圍內已有 {existingKeys.Count} 筆記錄");

                    // 只上傳新記錄（使用 Timestamp + Action 複合鍵比對）
                    var newLogs = powerLogs
                        .Where(log => {
                            string key = $"{log.Timestamp:yyyy-MM-dd HH:mm:ss}|{log.Action}";
                            return !existingKeys.Contains(key);
                        })
                        .ToList();

                    int duplicateCount = powerLogs.Count - newLogs.Count;
                    if (duplicateCount > 0)
                    {
                        LogService.Log($"[PowerLogService] 跳過 {duplicateCount} 筆已存在的記錄");
                    }

                    if (newLogs.Count > 0)
                    {
                        await db.PowerLogs.AddRangeAsync(newLogs);
                        int savedCount = await db.SaveChangesAsync();
                        LogService.Log($"[PowerLogService] ? 成功上傳 {savedCount} 筆新的開關機記錄（共收集 {powerLogs.Count} 筆，跳過 {duplicateCount} 筆重複）");
                        
                        // 顯示最早和最新的記錄
                        var earliest = newLogs.Min(l => l.Timestamp);
                        var latest = newLogs.Max(l => l.Timestamp);
                        LogService.Log($"[PowerLogService] 上傳範圍：{earliest:yyyy-MM-dd HH:mm:ss} 到 {latest:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        LogService.Log($"[PowerLogService] ? 所有記錄都已存在（共收集 {powerLogs.Count} 筆，全部跳過），沒有新記錄需要上傳");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"[PowerLogService] ? 上傳開關機記錄時發生錯誤：{ex.GetType().Name}");
                LogService.Log($"[PowerLogService] 錯誤訊息：{ex.Message}");
                if (ex.InnerException != null)
                {
                    LogService.Log($"[PowerLogService] 內部錯誤：{ex.InnerException.Message}");
                }
            }
        }
    }
}
