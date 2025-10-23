using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace collect_all.Models
{
    public class SoftwareInfo
    {
        [Required]
        [StringLength(50)]
        public string DeviceNo { get; set; } = string.Empty;
        
        [Required]
        [StringLength(255)]
        public string SoftwareName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string Version { get; set; } = string.Empty;
        
        [StringLength(255)]
        public string? Publisher { get; set; }
        
        public DateTime? InstallationDate { get; set; }
    }
}