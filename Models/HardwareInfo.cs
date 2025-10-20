using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace collect_all.Models
{
    public class HardwareInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string DeviceNo { get; set; } = string.Empty;
        public string Processor { get; set; } = string.Empty;
        public string Motherboard { get; set; } = string.Empty;
        public long MemoryTotalGB { get; set; }
        public long MemoryAvailableGB { get; set; }
        public string IPAddress { get; set; } = string.Empty;
        public DateTime? UpdateDate { get; set; }
        public DateTime? CreateDate { get; set; }
    }
}