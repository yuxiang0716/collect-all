// 檔案: Services/UserService.cs
using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using collect_all.Models;

namespace collect_all.Services
{
    public class UserService
    {
        private readonly string ConnString = "server=127.0.0.1;port=3306;database=systemmonitordb;user=users;password=users;";

        // 修改點：方法簽名不再需要 role 參數
        public async Task<(bool Success, string Message)> RegisterUserAsync(string username, string password, string company, string fullName)
        {
            PasswordService.CreatePasswordHash(password, out byte[] passwordHash, out byte[] passwordSalt);
            
            // 修改點：Role 被硬式編碼為 "客戶"
            var user = new User { Username = username, PasswordHash = passwordHash, PasswordSalt = passwordSalt, Company = company, FullName = fullName, Role = "客戶", CreateAt = DateTime.UtcNow, UpdateAt = DateTime.UtcNow };
            
            try
            {
                using (var conn = new MySqlConnection(ConnString))
                {
                    await conn.OpenAsync();
                    string query = "INSERT INTO Users (Username, PasswordHash, PasswordSalt, Company, FullName, Role, CreateAt, UpdateAt) VALUES (@Username, @PasswordHash, @PasswordSalt, @Company, @FullName, @Role, @CreateAt, @UpdateAt)";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Username", user.Username);
                        cmd.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
                        cmd.Parameters.AddWithValue("@PasswordSalt", user.PasswordSalt);
                        cmd.Parameters.AddWithValue("@Company", user.Company);
                        cmd.Parameters.AddWithValue("@FullName", user.FullName);
                        cmd.Parameters.AddWithValue("@Role", user.Role); // 這裡會傳入 "客戶"
                        cmd.Parameters.AddWithValue("@CreateAt", user.CreateAt);
                        cmd.Parameters.AddWithValue("@UpdateAt", user.UpdateAt);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                return (true, "註冊成功！");
            }
            catch (Exception ex) { return (false, $"註冊失敗：資料庫發生錯誤。 {ex.Message}"); }
        }

        // ... 其餘方法維持不變 ...
        public async Task<(bool Success, string Message)> DeleteUserAsync(string username)
        {
            try
            {
                using (var conn = new MySqlConnection(ConnString))
                {
                    await conn.OpenAsync();
                    string query = "DELETE FROM Users WHERE Username = @Username";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        return rowsAffected > 0 ? (true, $"帳號 {username} 已成功註銷。") : (false, "找不到要註銷的帳號。");
                    }
                }
            }
            catch (Exception ex) { return (false, $"註銷失敗：資料庫發生錯誤。 {ex.Message}"); }
        }

        public async Task<bool> UserExistsAsync(string username)
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
            catch { return true; }
        }
        
        public async Task<User?> GetUserDetailsAsync(string username)
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
                            if (await reader.ReadAsync())
                            {
                                return new User
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    Username = reader["Username"].ToString() ?? string.Empty,
                                    PasswordHash = (byte[])reader["PasswordHash"],
                                    PasswordSalt = (byte[])reader["PasswordSalt"]
                                };
                            }
                            return null;
                        }
                    }
                }
            }
            catch { return null; }
        }

        public async Task<(bool Success, string Message)> UpdatePasswordAsync(string username, string newPassword)
        {
            PasswordService.CreatePasswordHash(newPassword, out byte[] passwordHash, out byte[] passwordSalt);
            try
            {
                using (var conn = new MySqlConnection(ConnString))
                {
                    await conn.OpenAsync();
                    string query = "UPDATE Users SET PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt, UpdateAt = @UpdateAt WHERE Username = @Username";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);
                        cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                        cmd.Parameters.AddWithValue("@PasswordSalt", passwordSalt);
                        cmd.Parameters.AddWithValue("@UpdateAt", DateTime.UtcNow);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        return rowsAffected > 0 ? (true, "密碼已成功更新！") : (false, "更新密碼失敗，找不到該帳號。");
                    }
                }
            }
            catch (Exception ex) { return (false, $"更新密碼時發生資料庫錯誤: {ex.Message}"); }
        }

        public async Task<(bool Success, string Message, User? LoggedInUser)> LoginUserAsync(string username, string password)
        {
            var userDetails = await GetUserDetailsAsync(username);
            if (userDetails == null)
            {
                return (false, "帳號或密碼錯誤！", null);
            }
            if (!PasswordService.VerifyPasswordHash(password, userDetails.PasswordHash, userDetails.PasswordSalt))
            {
                return (false, "帳號或密碼錯誤！", null);
            }
            return (true, $"歡迎回來, {userDetails.Username}！", userDetails);
        }
    }
}