using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace collect_all.Models
{
    public class GraphicsCardInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string DeviceNo { get; set; } = string.Empty;
        public string CardName { get; set; } = string.Empty;
    }
}