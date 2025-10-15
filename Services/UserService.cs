// 檔案: Services/UserService.cs
using System;
using System.Threading.Tasks;
using System.Windows;
using MySql.Data.MySqlClient;
using collect_all.Models;

namespace collect_all.Services
{
    public class UserService
    {
        private readonly string ConnString = "server=127.0.0.1;port=3306;database=systemmonitordb;user=users;password=users;";

        public async Task<(bool Success, string Message)> RegisterUserAsync(string username, string password)
        {
            if (await UserExistsAsync(username))
            {
                return (false, "此帳號已經存在");
            }

            PasswordService.CreatePasswordHash(password, out byte[] passwordHash, out byte[] passwordSalt);

            var user = new User
            {
                Username = username,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                CreateAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow
            };

            try
            {
                using (var conn = new MySqlConnection(ConnString))
                {
                    await conn.OpenAsync();
                    string query = "INSERT INTO Users (Username, PasswordHash, PasswordSalt, CreateAt, UpdateAt) VALUES (@Username, @PasswordHash, @PasswordSalt, @CreateAt, @UpdateAt)";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Username", user.Username);

                        cmd.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
                        cmd.Parameters.AddWithValue("@PasswordSalt", user.PasswordSalt);
                        cmd.Parameters.AddWithValue("@CreateAt", user.CreateAt); // 補上這個參數
                        cmd.Parameters.AddWithValue("@UpdateAt", user.UpdateAt);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                return (true, "註冊成功！");
            }
            catch (Exception ex)
            {
                return (false, $"註冊失敗：資料庫發生錯誤。 {ex.Message}");
            }
        }

        private async Task<bool> UserExistsAsync(string username)
        {
            try
            {
                using (var conn = new MySqlConnection(ConnString))
                {
                    await conn.OpenAsync();
                    string query = "SELECT COUNT(1) FROM Users WHERE Username = @Username;";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);
                        var result = await cmd.ExecuteScalarAsync();
                        return Convert.ToInt32(result) > 0;
                    }
                }
            }
            catch
            {
                return true;
            }
        }

        public async Task<(bool Success, string Message, User? LoggedInUser)> LoginUserAsync(string username, string password)
        {
            try
            {
                using (var conn = new MySqlConnection(ConnString))
                {
                    await conn.OpenAsync();
                    string query = "SELECT Id, Username, PasswordHash, PasswordSalt FROM Users WHERE Username = @Username;";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!reader.HasRows)
                            {
                                return (false, "帳號或密碼錯誤！", null);
                            }
                            await reader.ReadAsync();

                            var storedHash = (byte[])reader["PasswordHash"];
                            var storedSalt = (byte[])reader["PasswordSalt"];
                            if (!PasswordService.VerifyPasswordHash(password, storedHash, storedSalt))
                            {
                                return (false, "帳號或密碼錯誤！", null);
                            }
                            var user = new User
                            {
                                Id = (int)reader["Id"],
                                Username = (string)reader["Username"]
                            };

                            return (true, $"歡迎回來, {user.Username}！", user);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"登入失敗：資料庫發生錯誤。 {ex.Message}", null);
            }
        }
    }
}