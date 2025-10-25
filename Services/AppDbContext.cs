using System;
using Microsoft.EntityFrameworkCore;
using collect_all.Models;

namespace collect_all.Services;

public class AppDbContext : DbContext
{
	private readonly string _connString;

	public DbSet<AlertInfo> AlertInfos { get; set; }

	public DbSet<Device> Devices { get; set; }

	public DbSet<DiskInfo> DiskInfos { get; set; }

	public DbSet<GraphicsCardInfo> GraphicsCardInfos { get; set; }

	public DbSet<HardwareInfo> HardwareInfos { get; set; }

	public DbSet<PowerLog> PowerLogs { get; set; }

	public DbSet<SoftwareInfo> SoftwareInfos { get; set; }

	public DbSet<User> UserAccounts { get; set; }

	public DbSet<MacAddressTable> MacAddressTables { get; set; }

	public DbSet<Settings> Settings { get; set; }

	public AppDbContext()
	{
		_connString = DbConfig.ConnectionString;
	}

	protected override void OnConfiguring(DbContextOptionsBuilder options)
	{
		if (string.IsNullOrWhiteSpace(_connString))
		{
			throw new InvalidOperationException("請在 DbConfig.ConnectionString 設定 MySQL 連線字串。");
		}
		options.UseMySql(_connString, ServerVersion.AutoDetect(_connString));
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<AlertInfo>().ToTable("alertinfos");
		modelBuilder.Entity<Device>().ToTable("devices");
		modelBuilder.Entity<DiskInfo>().ToTable("diskinfos");
		modelBuilder.Entity<GraphicsCardInfo>().ToTable("graphicscardinfos");
		modelBuilder.Entity<HardwareInfo>().ToTable("hardwareinfos");
		modelBuilder.Entity<PowerLog>().ToTable("powerlogs");
		modelBuilder.Entity<SoftwareInfo>().ToTable("softwareinfos");
		modelBuilder.Entity<User>().ToTable("useraccounts");
		modelBuilder.Entity<MacAddressTable>().ToTable("macaddresstable");
		modelBuilder.Entity<Settings>().ToTable("settings");
		modelBuilder.Entity<SoftwareInfo>().HasKey(s => new { s.DeviceNo, s.SoftwareName, s.Version });
	}
}
