using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace collect_all.Models
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Account { get; set; } = string.Empty;

        // BCrypt hash 存為文字
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        // DB schema 定義為 int
        public int Role { get; set; }

        [StringLength(100)]
        public string CompanyName { get; set; } = string.Empty;
    }
}
