using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace collect_all.Models
{
    public class MacAddressTable
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [StringLength(255)]
        public string MacAddress { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string User { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string DeviceId { get; set; } = string.Empty;
    }
}
