// 檔案: Models/BootEvent.cs
using System;

namespace collect_all.Models
{
    public class BootEvent
    {
        public DateTime Time { get; set; }
        public string EventType { get; set; } = string.Empty;
    }
}