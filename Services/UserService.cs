// 檔案: Services/UserService.cs
using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using collect_all.Models;

namespace collect_all.Services
{
    public class UserService
    {
        // 連接字串
        private readonly string ConnString = DbConfig.ConnectionString;

        // 取得使用者詳細資料（含 PasswordHash 為字串）
        public async Task<User?> GetUserDetailsAsync(string account)
        {
            try
            {
                using (var conn = new MySqlConnection(ConnString))
                {
                    await conn.OpenAsync();
                    string query = "SELECT UserId, Account, PasswordHash, Role, CompanyName FROM useraccounts WHERE Account = @Account;";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Account", account);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new User
                                {
                                    UserId = Convert.ToInt32(reader["UserId"]),
                                    Account = reader["Account"]?.ToString() ?? string.Empty,
                                    PasswordHash = reader["PasswordHash"]?.ToString() ?? string.Empty,
                                    Role = reader["Role"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Role"]),
                                    CompanyName = reader["CompanyName"]?.ToString() ?? string.Empty
                                };
                            }
                            return null;
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        // Login 使用 BCrypt 驗證
        public async Task<User?> Login(string account, string password)
        {
            var user = await GetUserDetailsAsync(account);

            if (user == null || !PasswordService.VerifyPasswordHash(password, user.PasswordHash))
            {
                return null;
            }

            return user;
        }
    }
}