// 檔案：Models/Software.cs (放入 collect_all.Models，供 ViewModel 使用)

using System;
using System.ComponentModel.DataAnnotations;

namespace collect_all.Models
{
    public class Software
    {
        [Key]
        public int Number { get; set; }
        
        public string DisplayName { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string InstallDate { get; set; } = string.Empty;
        public string DisplayVersion { get; set; } = string.Empty;
        public string LastUpdate { get; set; } = string.Empty;
    }
}