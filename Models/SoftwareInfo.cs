using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace collect_all.Models
{
    public class SoftwareInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string DeviceNo { get; set; } = string.Empty;
        public string SoftwareName { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public DateTime? InstallationDate { get; set; }
        public string Version { get; set; } = string.Empty;
    }
}