// 檔案: Services/DataSendService.cs (由 SoftwareSendService.cs 升級而來)
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using collect_all.Models;
using Microsoft.EntityFrameworkCore;

namespace collect_all.Services
{
    public static class DataSendService
    {
        // 初始化資料庫，確保資料表都已建立
        public static void InitializeDatabase()
        {
            using (var db = new AppDbContext())
            {
                db.Database.EnsureCreated();
            }
        }

        // 傳送軟體資訊 (保留舊功能)
        public static async Task SendSoftwareAsync(List<Software> softwareList)
        {
            if (softwareList == null || softwareList.Count == 0) return;
            try
            {
                using (var db = new AppDbContext())
                {
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM SoftwareInfo");
                    await db.SoftwareInfo.AddRangeAsync(softwareList);
                    await db.SaveChangesAsync();
                    Console.WriteLine($"已成功將 {softwareList.Count} 筆軟體資訊傳送到資料庫。");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"傳送軟體資訊到資料庫時發生錯誤：{ex.Message}");
            }
        }
        
        // *** 新增的功能：傳送系統硬體資訊 ***
        public static async Task SendSystemInfoAsync(List<SystemInfoEntry> systemInfoList)
        {
            if (systemInfoList == null || systemInfoList.Count == 0) return;
            try
            {
                using (var db = new AppDbContext())
                {
                    // 每次都先清空舊資料，再寫入最新的
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM SystemInfo");
                    await db.SystemInfo.AddRangeAsync(systemInfoList);
                    await db.SaveChangesAsync();
                    Console.WriteLine($"已成功將 {systemInfoList.Count} 筆系統硬體資訊傳送到資料庫。");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"傳送系統硬體資訊到資料庫時發生錯誤：{ex.Message}");
            }
        }
    }
}