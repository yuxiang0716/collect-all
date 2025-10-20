// 檔案: Services/DataAccess.cs
using Microsoft.EntityFrameworkCore;
using collect_all.Models;

namespace collect_all.Services
{
    // 這個類別專門用來定義我們的資料庫結構
    public class AppDbContext : DbContext
    {
        // 定義 DbSet（保留或新增所有你需要的 model）
        public DbSet<AlertInfo> AlertInfos { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<DiskInfo> DiskInfos { get; set; }
        public DbSet<GraphicsCardInfo> GraphicsCardInfos { get; set; }
        public DbSet<HardwareInfo> HardwareInfos { get; set; }
        public DbSet<PowerLog> PowerLogs { get; set; }
        public DbSet<SoftwareInfo> SoftwareInfos { get; set; }
        public DbSet<User> UserAccounts { get; set; }

        private readonly string _connString;

        public AppDbContext()
        {
            _connString = DbConfig.ConnectionString;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (string.IsNullOrWhiteSpace(_connString))
                throw new InvalidOperationException("請在 DbConfig.ConnectionString 設定 MySQL 連線字串。");

            // 使用 Pomelo 的 UseMySql，ServerVersion.AutoDetect 會自動偵測版本
            options.UseMySql(_connString, ServerVersion.AutoDetect(_connString));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 將 model 對應到你 MySQL 中既有的 table names
            modelBuilder.Entity<AlertInfo>().ToTable("alertinfos");
            modelBuilder.Entity<Device>().ToTable("devices");
            modelBuilder.Entity<DiskInfo>().ToTable("diskinfos");
            modelBuilder.Entity<GraphicsCardInfo>().ToTable("graphicscardinfos");
            modelBuilder.Entity<HardwareInfo>().ToTable("hardwareinfos");
            modelBuilder.Entity<PowerLog>().ToTable("powerlogs");
            modelBuilder.Entity<SoftwareInfo>().ToTable("softwareinfos");
            modelBuilder.Entity<User>().ToTable("useraccounts");

            // 若需要對應欄位名稱（例如 model 屬性名與 DB 欄位名不同），在這裡加入：
            // modelBuilder.Entity<User>().Property(u => u.UserId).HasColumnName("UserId");
            // modelBuilder.Entity<User>().Property(u => u.CompanyName).HasColumnName("CompanyName");
        }
    }
}