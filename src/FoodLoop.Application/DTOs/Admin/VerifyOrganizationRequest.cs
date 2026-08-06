using System.ComponentModel.DataAnnotations;

namespace FoodLoop.Application.DTOs.Admin;

public class VerifyOrganizationRequest
{
    /// <summary>Approved or Rejected.</summary>
    [Required]
    [RegularExpression("^(Approved|Rejected)$", ErrorMessage = "Action must be 'Approved' or 'Rejected'.")]
    public string Action { get; set; } = string.Empty;

    /// <summary>Optional note visible to the organization owner explaining the decision.</summary>
    [MaxLength(500)]
    public string? Note { get; set; }
}

