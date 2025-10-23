// 檔案: Services/DataSendService.cs (由 SoftwareSendService.cs 升級而來)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using collect_all.Models;
using Microsoft.EntityFrameworkCore;

namespace collect_all.Services
{
    public static class DataSendService
    {
        // 保留初始化介面供 App.xaml.cs 呼叫，但維持空實作以避免修改或建立資料表
        public static void InitializeDatabase()
        {
            // no-op：刻意不呼叫 EnsureCreated()，避免改動既有資料庫
        }

        // 接受 ViewModel 的 Software 類別，並轉換為 SoftwareInfo 再寫入 MySQL
        public static async Task SendSoftwareAsync(List<Software> softwareList)
        {
            LogService.Log($"[SendSoftwareAsync] 開始上傳軟體資訊，共 {softwareList?.Count ?? 0} 筆");
            
            if (softwareList == null || softwareList.Count == 0)
            {
                LogService.Log("[SendSoftwareAsync] 軟體清單為空，取消上傳");
                return;
            }

            try
            {
                // 檢查使用者登入狀態
                var currentUser = AuthenticationService.Instance.CurrentUser;
                if (currentUser == null)
                {
                    LogService.Log("[SendSoftwareAsync] 使用者未登入，取消上傳");
                    return;
                }
                LogService.Log($"[SendSoftwareAsync] 目前登入使用者: {currentUser.Account}");

                // 取得裝置編號
                string deviceNo = await GetDeviceNoAsync();
                LogService.Log($"[SendSoftwareAsync] 取得的裝置編號: {deviceNo}");
                
                if (string.IsNullOrEmpty(deviceNo))
                {
                    LogService.Log("[SendSoftwareAsync] 無法取得裝置編號，無法上傳軟體資訊");
                    return;
                }

                using (var db = new AppDbContext())
                {
                    LogService.Log("[SendSoftwareAsync] 資料庫連線成功");
                    
                    // 使用原始 SQL 直接刪除該裝置的所有舊資料（避免 EF 追蹤問題）
                    int deletedCount = await db.Database.ExecuteSqlRawAsync(
                        "DELETE FROM softwareinfos WHERE DeviceNo = {0}", 
                        deviceNo
                    );
                    
                    if (deletedCount > 0)
                    {
                        LogService.Log($"[SendSoftwareAsync] 已刪除裝置 {deviceNo} 的 {deletedCount} 筆舊軟體資料");
                    }
                    else
                    {
                        LogService.Log($"[SendSoftwareAsync] 裝置 {deviceNo} 無舊資料");
                    }

                    // 過濾並轉換資料，同時進行去重（使用 Dictionary 確保唯一性）
                    var uniqueRecords = new Dictionary<string, SoftwareInfo>();
                    int duplicateInUpload = 0;
                    
                    foreach (var software in softwareList)
                    {
                        // 跳過空白名稱
                        if (string.IsNullOrWhiteSpace(software.DisplayName))
                            continue;
                        
                        string cleanedName = software.DisplayName.Trim();
                        string cleanedVersion = NormalizeVersion(software.DisplayVersion);
                        
                        // 建立唯一 key
                        string uniqueKey = $"{cleanedName}|{cleanedVersion}";
                        
                        if (uniqueRecords.ContainsKey(uniqueKey))
                        {
                            duplicateInUpload++;
                            LogService.Log($"[SendSoftwareAsync] 上傳階段發現重複: {cleanedName} (版本: {cleanedVersion})");
                            continue;
                        }
                        
                        uniqueRecords[uniqueKey] = new SoftwareInfo
                        {
                            DeviceNo = deviceNo,
                            SoftwareName = cleanedName,
                            Version = cleanedVersion,
                            Publisher = NormalizeToNull(software.Publisher),
                            InstallationDate = ParseNullableDate(software.InstallDate)
                        };
                    }
                    
                    var newRecords = uniqueRecords.Values.ToList();

                    int filteredCount = softwareList.Count - newRecords.Count;
                    if (filteredCount > 0)
                    {
                        LogService.Log($"[SendSoftwareAsync] 已過濾 {filteredCount} 筆資料（空白: {filteredCount - duplicateInUpload}, 重複: {duplicateInUpload}）");
                    }

                    LogService.Log($"[SendSoftwareAsync] 準備新增 {newRecords.Count} 筆新資料");

                    if (newRecords.Count > 0)
                    {
                        await db.SoftwareInfos.AddRangeAsync(newRecords);
                        LogService.Log("[SendSoftwareAsync] 資料已加入 Context，準備儲存");
                        
                        int savedCount = await db.SaveChangesAsync();
                        LogService.Log($"[SendSoftwareAsync] ✓ 成功儲存 {savedCount} 筆變更 (裝置編號: {deviceNo})");
                    }
                    else
                    {
                        LogService.Log("[SendSoftwareAsync] 沒有有效的新資料可上傳");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"[SendSoftwareAsync] ✗ 錯誤：{ex.GetType().Name}");
                LogService.Log($"[SendSoftwareAsync] 錯誤訊息：{ex.Message}");
                if (ex.InnerException != null)
                {
                    LogService.Log($"[SendSoftwareAsync] 內部錯誤：{ex.InnerException.Message}");
                }
            }
        }

        // 如果你的 MySQL 中沒有 system info table，這個方法會改為不寫入 DB（avoiding 錯誤）
        public static async Task SendSystemInfoAsync(List<SystemInfoEntry> systemInfoList)
        {
            if (systemInfoList == null || systemInfoList.Count == 0) return;
            try
            {
                // 如果你想把系統資訊也存到資料庫，先在 MySQL 新增對應 table，或告訴我表名及 schema，我會協助映射
                LogService.Log("SendSystemInfoAsync：MySQL 中無對應表或暫不寫入，已略過寫入步驟。");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogService.Log($"傳送系統資訊到 MySQL 時發生錯誤：{ex.Message}");
            }
        }

        // 取得裝置編號（從 MAC 位址查詢）
        private static async Task<string> GetDeviceNoAsync()
        {
            try
            {
                LogService.Log("[GetDeviceNoAsync] 開始取得裝置編號");
                var macService = new SystemMacLoginService();
                string mac = macService.GetPrimaryMac();
                
                LogService.Log($"[GetDeviceNoAsync] 取得的 MAC 位址: {mac}");
                
                if (string.IsNullOrEmpty(mac))
                {
                    LogService.Log("[GetDeviceNoAsync] MAC 位址為空");
                    return string.Empty;
                }

                var (found, macRecord) = await macService.CheckMacInTableAsync(mac);
                
                LogService.Log($"[GetDeviceNoAsync] 查詢結果 - 找到: {found}, DeviceId: {macRecord?.DeviceId ?? "null"}");
                
                if (found && macRecord != null)
                {
                    return macRecord.DeviceId;
                }
                
                LogService.Log("[GetDeviceNoAsync] 未在資料庫中找到對應的 MAC 記錄");
            }
            catch (Exception ex)
            {
                LogService.Log($"[GetDeviceNoAsync] 取得裝置編號時發生錯誤：{ex.Message}");
            }
            
            return string.Empty;
        }

        // 標準化 Version：確保不為 null，空值轉為空字串
        private static string NormalizeVersion(string? version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return string.Empty;
            
            var normalized = version.Trim();
            
            // 如果是無效值，返回空字串
            if (normalized.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("不支援", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            
            return normalized;
        }

        // 將空值、"N/A"、"不支援" 等轉為 null（用於可為 null 的欄位）
        private static string? NormalizeToNull(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            
            var normalized = value.Trim();
            
            // 檢查是否為無效值
            if (normalized.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("不支援", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            
            return normalized;
        }

        private static DateTime? ParseNullableDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            
            var normalized = s.Trim();
            
            // 檢查是否為無效值
            if (normalized.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("不支援", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            
            if (DateTime.TryParse(normalized, out var dt)) return dt;
            
            // 嘗試其他格式 (yyyy/MM/dd 等)
            if (DateTime.TryParseExact(normalized, new[] { "yyyy/MM/dd", "yyyy-MM-dd", "yyyyMMdd" }, 
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
                return dt;
            
            return null;
        }
    }
}