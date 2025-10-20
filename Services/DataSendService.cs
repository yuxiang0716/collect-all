// 檔案: Services/DataSendService.cs (由 SoftwareSendService.cs 升級而來)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using collect_all.Models;
using Microsoft.EntityFrameworkCore;

namespace collect_all.Services
{
    public static class DataSendService
    {
        // 保留初始化介面供 App.xaml.cs 呼叫，但維持空實作以避免修改或建立資料表
        public static void InitializeDatabase()
        {
            // no-op：刻意不呼叫 EnsureCreated()，避免改動既有資料庫
        }

        // 接受 ViewModel 的 Software 類別，並轉換為 SoftwareInfo 再寫入 MySQL
        public static async Task SendSoftwareAsync(List<Software> softwareList)
        {
            if (softwareList == null || softwareList.Count == 0) return;

            try
            {
                using (var db = new AppDbContext())
                {
                    // 刪除舊資料再寫入（注意：不會 DROP 或 ALTER table）
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM softwareinfos");

                    string deviceNo = Environment.MachineName ?? string.Empty;

                    var entities = softwareList.Select(s => new SoftwareInfo
                    {
                        DeviceNo = deviceNo,
                        SoftwareName = s.DisplayName ?? string.Empty,
                        Publisher = s.Publisher ?? string.Empty,
                        InstallationDate = ParseNullableDate(s.InstallDate),
                        Version = s.DisplayVersion ?? string.Empty
                    }).ToList();

                    if (entities.Count > 0)
                    {
                        await db.SoftwareInfos.AddRangeAsync(entities);
                        await db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"傳送軟體資訊到 MySQL 時發生錯誤：{ex.Message}");
            }
        }

        // 如果你的 MySQL 中沒有 system info table，這個方法會改為不寫入 DB（避免錯誤）
        public static async Task SendSystemInfoAsync(List<SystemInfoEntry> systemInfoList)
        {
            if (systemInfoList == null || systemInfoList.Count == 0) return;
            try
            {
                // 如果你想把系統資訊也存到資料庫，先在 MySQL 新增對應 table，或告訴我表名及 schema，我會協助映射
                Console.WriteLine("SendSystemInfoAsync：MySQL 中無對應表或暫不寫入，已略過寫入步驟。");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"傳送系統資訊到 MySQL 時發生錯誤：{ex.Message}");
            }
        }

        private static DateTime? ParseNullableDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParse(s, out var dt)) return dt;
            // 嘗試其他格式 (yyyy/MM/dd 等)
            if (DateTime.TryParseExact(s, new[] { "yyyy/MM/dd", "yyyy-MM-dd", "yyyyMMdd" }, 
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
                return dt;
            return null;
        }
    }
}