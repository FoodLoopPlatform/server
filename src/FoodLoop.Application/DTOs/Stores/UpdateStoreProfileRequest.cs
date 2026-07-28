using System.ComponentModel.DataAnnotations;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.DTOs.Stores;

/// <summary>PATCH /stores/me — all fields optional; only supplied fields are updated.</summary>
public class UpdateStoreProfileRequest
{
    [MaxLength(150)]
    public string? Name { get; set; }

    [MaxLength(150)]
    public string? NameAr { get; set; }

    public string? Description { get; set; }

    public string? DescriptionAr { get; set; }

    public BusinessCategory? BusinessCategory { get; set; }

    /// <summary>URL of the store logo image.</summary>
    public string? Logo { get; set; }
}
