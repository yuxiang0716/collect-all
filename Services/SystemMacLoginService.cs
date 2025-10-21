using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Collections.Generic;
using collect_all.Services;
using collect_all.Models;

namespace collect_all.Services
{
    public class SystemMacLoginService
    {
        private readonly MacAddressService _macService;
        private readonly UserService _userService;

        public SystemMacLoginService()
        {
            _macService = new MacAddressService();
            _userService = new UserService();
        }

        public string GetPrimaryMac()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    var bytes = ni.GetPhysicalAddress().GetAddressBytes();
                    if (bytes == null || bytes.Length == 0) continue;
                    return string.Join("-", Array.ConvertAll(bytes, b => b.ToString("X2"))).ToUpperInvariant();
                }
            }
            catch { }
            return string.Empty;
        }

        // Returns: (macFoundInTable, macRecord)
        public async Task<(bool, MacAddressTable?)> CheckMacInTableAsync(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return (false, null);
            var rec = await _macService.GetByMacAsync(mac);
            return (rec != null, rec);
        }

        public async Task<User?> LoginByCredentialsAsync(string account, string password)
        {
            return await _userService.Login(account, password);
        }

        // Try to assign mac to any empty row for user; retries through available rows
        public async Task<(bool Success, string Message)> AssignMacIfAvailableAsync(string account, string mac)
        {
            var emptyRows = await _macService.GetEmptyForUserAsync(account);
            if (emptyRows == null || emptyRows.Count == 0) return (false, "無剩餘裝置可使用");

            foreach (var row in emptyRows)
            {
                var assigned = await _macService.AssignMacToRowAsync(row.Id, mac);
                if (assigned) return (true, "已指派裝置");
                // else try next row
            }

            return (false, "指派失敗");
        }
    }
}
