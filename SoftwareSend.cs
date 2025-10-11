// 檔案：SoftwareSend.cs
// 職責：專門負責將資料上傳到資料庫。這是要交給您朋友修改的檔案。

using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace collect_all
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

    /// <summary>
    /// 負責將資料傳送到資料庫的類別。
    /// </summary>
    public class SoftwareSend // <--- 名稱已更改
    {
        /// <summary>
        /// 初始化資料庫。
        /// </summary>
        public static void Initialize()
        {
            using (var db = new AppDbContext())
            {
                db.Database.EnsureCreated();
            }
        }

        /// <summary>
        /// 將軟體資訊列表以非同步方式傳送到資料庫。
        /// </summary>
        public static async Task SendAsync(List<Software> softwareList) // <--- 名稱已更改
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