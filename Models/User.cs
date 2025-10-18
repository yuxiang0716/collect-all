using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace collect_all.Models
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string Company { get; set; } = string.Empty; // 所屬公司

        [Required]
        [StringLength(255)]
        public string FullName { get; set; } = string.Empty; // 使用者全名

        [Required]
        [StringLength(50)]
        public string Role { get; set; } = string.Empty; // 權限角色

        [Required]
        public  byte[] PasswordHash { get; set; } = Array.Empty<byte>();

        [Required]
        public  byte[] PasswordSalt { get; set; } = Array.Empty<byte>();

        public DateTime CreateAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdateAt { get; set; }

        

    }
}
