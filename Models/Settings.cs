using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace collect_all.Models;

public class Settings
{
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	[Required]
	[StringLength(255)]
	public string CompanyName { get; set; } = "admin";

	public int? AlertTemp { get; set; }

	public int? AlertUsage { get; set; }

	public int? HardwareDetectTime { get; set; }

	public int? NetworkDetectTime { get; set; }

	[StringLength(255)]
	public string? UpdateUser { get; set; }
}
