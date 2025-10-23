// 檔案: Services/GraphicsCardInfoService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using collect_all.Models;
using Microsoft.EntityFrameworkCore;

namespace collect_all.Services
{
    /// <summary>
    /// 顯卡資訊服務 - 處理顯卡資訊的上傳與同步
    /// </summary>
    public class GraphicsCardInfoService
    {
        /// <summary>
        /// 上傳或更新顯卡資訊（使用已收集的資料，支援多張顯卡）
        /// </summary>
        public async Task UploadOrUpdateGraphicsCardsAsync(string deviceNo, List<string> gpuNames)
        {
            if (string.IsNullOrEmpty(deviceNo))
            {
                LogService.Log("[GraphicsCardInfoService] 裝置編號為空，無法上傳顯卡資訊");
                return;
            }

            if (gpuNames == null || gpuNames.Count == 0)
            {
                LogService.Log("[GraphicsCardInfoService] 沒有顯卡資訊可上傳");
                return;
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    LogService.Log("[GraphicsCardInfoService] 資料庫連線成功");

                    // 去重並過濾空白
                    var uniqueGpuNames = gpuNames
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Select(name => name.Trim())
                        .Distinct()
                        .ToList();

                    LogService.Log($"[GraphicsCardInfoService] 收集到 {uniqueGpuNames.Count} 個不重複的顯卡：");
                    foreach (var gpuName in uniqueGpuNames)
                    {
                        LogService.Log($"  - {gpuName}");
                    }

                    // 查詢資料庫中該裝置的所有顯卡記錄
                    var existingCards = await db.GraphicsCardInfos
                        .Where(g => g.DeviceNo == deviceNo)
                        .ToListAsync();

                    // 取得現有顯卡名稱
                    var existingNames = existingCards.Select(c => c.CardName).ToHashSet();

                    // 找出需要新增的顯卡（資料庫中沒有的）
                    var cardsToAdd = uniqueGpuNames
                        .Where(name => !existingNames.Contains(name))
                        .Select(name => new GraphicsCardInfo
                        {
                            DeviceNo = deviceNo,
                            CardName = name
                        })
                        .ToList();

                    // 找出需要刪除的顯卡（不在當前清單中的）
                    var cardsToRemove = existingCards
                        .Where(card => !uniqueGpuNames.Contains(card.CardName))
                        .ToList();

                    // 執行新增
                    if (cardsToAdd.Count > 0)
                    {
                        await db.GraphicsCardInfos.AddRangeAsync(cardsToAdd);
                        LogService.Log($"[GraphicsCardInfoService] 新增 {cardsToAdd.Count} 張顯卡：");
                        foreach (var card in cardsToAdd)
                        {
                            LogService.Log($"  + {card.CardName}");
                        }
                    }

                    // 執行刪除
                    if (cardsToRemove.Count > 0)
                    {
                        db.GraphicsCardInfos.RemoveRange(cardsToRemove);
                        LogService.Log($"[GraphicsCardInfoService] 移除 {cardsToRemove.Count} 張顯卡：");
                        foreach (var card in cardsToRemove)
                        {
                            LogService.Log($"  - {card.CardName}");
                        }
                    }

                    // 儲存變更
                    if (cardsToAdd.Count > 0 || cardsToRemove.Count > 0)
                    {
                        int savedCount = await db.SaveChangesAsync();
                        LogService.Log($"[GraphicsCardInfoService] ? 成功更新顯卡資訊（裝置編號: {deviceNo}，變更 {savedCount} 筆）");
                    }
                    else
                    {
                        LogService.Log($"[GraphicsCardInfoService] ? 顯卡資訊無變動，不需要更新（裝置編號: {deviceNo}）");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"[GraphicsCardInfoService] ? 上傳顯卡資訊時發生錯誤：{ex.GetType().Name}");
                LogService.Log($"[GraphicsCardInfoService] 錯誤訊息：{ex.Message}");
                if (ex.InnerException != null)
                {
                    LogService.Log($"[GraphicsCardInfoService] 內部錯誤：{ex.InnerException.Message}");
                }
            }
        }
    }
}
