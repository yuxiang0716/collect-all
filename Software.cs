// 檔案：Software.cs (修正後版本)

using System;
using System.ComponentModel.DataAnnotations; // <--- 1. 新增這一行

namespace collect_all
{
    public class Software
    {
        [Key] // <--- 2. 加上這個 [Key] 屬性，告訴資料庫 Number 是主鍵
        public int Number { get; set; }
        
        public string DisplayName { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string InstallDate { get; set; } = string.Empty;
        public string DisplayVersion { get; set; } = string.Empty;
        public string LastUpdate { get; set; } = string.Empty;
    }
}