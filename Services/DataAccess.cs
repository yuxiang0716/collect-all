// 檔案: Services/DataAccess.cs
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using collect_all.Models;

namespace collect_all.Services
{
    // 這個類別專門用來定義我們的資料庫結構
    public class AppDbContext : DbContext
    {
        // 定義資料庫中的兩個資料表：一個存軟體，一個存系統硬體資訊
        public DbSet<Software> SoftwareInfo { get; set; }
        public DbSet<SystemInfoEntry> SystemInfo { get; set; }

        private readonly string _dbPath;

        public AppDbContext()
        {
            // 資料庫檔案會存放在桌面的 "collect_all_data" 資料夾中
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "collect_all_data");
            Directory.CreateDirectory(folderPath);
            _dbPath = Path.Combine(folderPath, "collection.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={_dbPath}");
    }
}