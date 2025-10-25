using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace collect_all.Models
{
    public class DiskInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string DeviceNo { get; set; } = string.Empty;
        public string SlotName { get; set; } = string.Empty;
        public float TotalCapacityGB { get; set; }
        public float AvailableCapacityGB { get; set; }
        public string DeviceInfoDeviceNo { get; set; } = string.Empty;
    }
}