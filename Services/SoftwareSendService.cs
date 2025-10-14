// 檔案: Services/SoftwareSendService.cs
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using collect_all.Models; // <-- 引用 Models

namespace collect_all.Services // <-- 更新命名空間
{
    #region 資料庫定義 (EF Core)
    public class AppDbContext : DbContext
    {
        public DbSet<Software> SoftwareInfo { get; set; }
        private readonly string _dbPath;
        public AppDbContext()
        {
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "collect_all_data");
            Directory.CreateDirectory(folderPath);
            _dbPath = Path.Combine(folderPath, "collection.db");
        }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={_dbPath}");
    }
    #endregion

    public class SoftwareSendService
    {
        public static void Initialize()
        {
            using (var db = new AppDbContext())
            {
                db.Database.EnsureCreated();
            }
        }

        public static async Task SendAsync(List<Software> softwareList)
        {
            if (softwareList == null || softwareList.Count == 0)
            {
                Console.WriteLine("沒有資料可傳送。");
                return;
            }
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
                Console.WriteLine($"傳送到資料庫時發生錯誤：{ex.Message}");
            }
        }
    }
}