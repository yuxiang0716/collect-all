// 檔案: Models/SystemInfoEntry.cs
using System.ComponentModel.DataAnnotations;

namespace collect_all.Models
{
    public class SystemInfoEntry
    {
        [Key]
        public int Id { get; set; } // 自動產生的主鍵
        public string Category { get; set; } = string.Empty; // 分類 (例如: 核心硬體規格)
        public string Item { get; set; } = string.Empty;     // 項目 (例如: CPU 型號)
        public string Value { get; set; } = string.Empty;    // 數值
    }
}