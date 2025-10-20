using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace collect_all.Models
{
    public class AlertInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string DeviceNo { get; set; } = string.Empty;
        public DateTime? AlertDate { get; set; }
        public double? CpuT { get; set; }
        public double? MotherboardT { get; set; }
        public double? GpuT { get; set; }
        public double? HddT { get; set; }
        public double? CpuU { get; set; }
        public double? MemoryU { get; set; }
        public double? GpuU { get; set; }
        public double? HddU { get; set; }
    }
}