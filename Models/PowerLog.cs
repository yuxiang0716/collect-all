using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace collect_all.Models
{
    public class PowerLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string DeviceNo { get; set; } = string.Empty;
        public DateTime? Timestamp { get; set; }
        public string Action { get; set; } = string.Empty;
    }
}