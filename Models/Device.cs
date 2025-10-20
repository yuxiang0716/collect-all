using System;
using System.ComponentModel.DataAnnotations;

namespace collect_all.Models
{
    public class Device
    {
        [Key]
        [StringLength(50)]
        public string DeviceNo { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        [StringLength(100)]
        public string ComputerName { get; set; } = string.Empty;
        [StringLength(100)]
        public string CompanyName { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public int SoftwareCount { get; set; }
        public string User { get; set; } = string.Empty;
        public string Initializer { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime? RegistrationDate { get; set; }
        public string RegistrationStatus { get; set; } = string.Empty;
    }
}