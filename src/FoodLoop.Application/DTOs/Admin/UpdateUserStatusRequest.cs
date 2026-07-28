using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Application.DTOs.Admin;

public class UpdateUserStatusRequest
{
    /// <summary>One of: Active | Suspended | Banned</summary>
    [Required]
    [RegularExpression("^(Active|Suspended|Banned)$",
        ErrorMessage = "Status must be 'Active', 'Suspended', or 'Banned'.")]
    public string Status { get; set; } = string.Empty;
}
