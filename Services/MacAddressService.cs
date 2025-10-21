using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using collect_all.Models;

namespace collect_all.Services
{
    public class MacAddressService
    {
        private readonly string ConnString = DbConfig.ConnectionString;

        private static string NormalizeMacForStorage(string? mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return string.Empty;
            var s = mac.ToUpperInvariant();
            s = s.Replace("-", "").Replace(":", "").Replace(".", "").Replace(" ", "");
            return s;
        }

        // Public helper: format any MAC (raw or normalized) into display format AA-BB-CC-DD-EE-FF
        public static string FormatMacForDisplay(string? mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return string.Empty;
            var norm = NormalizeMacForStorage(mac);
            if (norm.Length != 12) return mac?.ToUpperInvariant() ?? string.Empty;
            return string.Join("-", new[] { norm.Substring(0,2), norm.Substring(2,2), norm.Substring(4,2), norm.Substring(6,2), norm.Substring(8,2), norm.Substring(10,2) });
        }

        public async Task<MacAddressTable?> GetByMacAsync(string mac)
        {
            try
            {
                var norm = NormalizeMacForStorage(mac);
                using (var conn = new MySqlConnection(ConnString))
                {
                    await conn.OpenAsync();
                    // Compare normalized MAC in DB by removing common separators and uppercasing
                    string query = "SELECT Id, MacAddress, User, DeviceId FROM macaddresstable WHERE UPPER(REPLACE(REPLACE(REPLACE(REPLACE(MacAddress,'-',''),':',''),'.',''),' ','')) = @Mac LIMIT 1;";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Mac", norm);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new MacAddressTable
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    MacAddress = reader["MacAddress"]?.ToString() ?? string.Empty,
                                    User = reader["User"]?.ToString() ?? string.Empty,
                                    DeviceId = reader["DeviceId"]?.ToString() ?? string.Empty
                                };
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public async Task<MacAddressTable?> GetFirstEmptyForUserAsync(string user)
        {
            try
            {
                using (var conn = new MySqlConnection(ConnString))
                {
                    await conn.OpenAsync();
                    string query = "SELECT Id, MacAddress, User, DeviceId FROM macaddresstable WHERE `User` = @User AND (MacAddress IS NULL OR MacAddress = '') LIMIT 1;";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@User", user);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new MacAddressTable
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    MacAddress = reader["MacAddress"]?.ToString() ?? string.Empty,
                                    User = reader["User"]?.ToString() ?? string.Empty,
                                    DeviceId = reader["DeviceId"]?.ToString() ?? string.Empty
                                };
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        // Return all empty rows for a user
        public async Task<List<MacAddressTable>> GetEmptyForUserAsync(string user)
        {
            var list = new List<MacAddressTable>();
            try
            {
                using (var conn = new MySqlConnection(ConnString))
                {
                    await conn.OpenAsync();
                    string query = "SELECT Id, MacAddress, User, DeviceId FROM macaddresstable WHERE `User` = @User AND (MacAddress IS NULL OR MacAddress = '') ORDER BY Id;";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@User", user);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new MacAddressTable
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    MacAddress = reader["MacAddress"]?.ToString() ?? string.Empty,
                                    User = reader["User"]?.ToString() ?? string.Empty,
                                    DeviceId = reader["DeviceId"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        public async Task<bool> AssignMacToRowAsync(int id, string mac)
        {
            try
            {
                var norm = NormalizeMacForStorage(mac);
                using (var conn = new MySqlConnection(ConnString))
                {
                    await conn.OpenAsync();
                    // Atomic update: only set MacAddress if it is currently NULL or empty
                    // Store normalized MAC value
                    string query = "UPDATE macaddresstable SET MacAddress = @Mac WHERE Id = @Id AND (MacAddress IS NULL OR MacAddress = '')";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Mac", norm);
                        cmd.Parameters.AddWithValue("@Id", id);
                        int rows = await cmd.ExecuteNonQueryAsync();
                        return rows > 0;
                    }
                }
            }
            catch { return false; }
        }
    }
}
