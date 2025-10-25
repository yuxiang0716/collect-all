using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using collect_all.Models;

namespace collect_all.Services;

public class SettingsService
{
	public async Task<(int NetworkDetectTime, int HardwareDetectTime)> GetSettingsAsync(string companyName)
	{
		try
		{
			using AppDbContext db = new AppDbContext();
			Settings? companySetting = await (from s in db.Settings
				where s.CompanyName == companyName
				orderby s.Id descending
				select s).FirstOrDefaultAsync();
			if (companySetting != null && companySetting.NetworkDetectTime.HasValue && companySetting.HardwareDetectTime.HasValue)
			{
				LogService.Log("[SettingsService] �ϥΤ��q " + companyName + " ���]�w");
				return (NetworkDetectTime: companySetting.NetworkDetectTime.Value, HardwareDetectTime: companySetting.HardwareDetectTime.Value);
			}
			Settings? adminSetting = await (from s in db.Settings
				where s.CompanyName == "admin"
				orderby s.Id descending
				select s).FirstOrDefaultAsync();
			if (adminSetting != null && adminSetting.NetworkDetectTime.HasValue && adminSetting.HardwareDetectTime.HasValue)
			{
				LogService.Log("[SettingsService] �ϥ� admin ���]�w");
				return (NetworkDetectTime: adminSetting.NetworkDetectTime.Value, HardwareDetectTime: adminSetting.HardwareDetectTime.Value);
			}
			LogService.Log("[SettingsService] �ϥιw�]��");
			return (NetworkDetectTime: 1, HardwareDetectTime: 30);
		}
		catch (Exception ex)
		{
			LogService.Log("[SettingsService] ���o�]�w�ɵo�Ϳ��~�G" + ex.Message);
			return (NetworkDetectTime: 1, HardwareDetectTime: 30);
		}
	}
}
