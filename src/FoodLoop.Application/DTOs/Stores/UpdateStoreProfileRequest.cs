using FoodLoop.Application.Common.Models;
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

    /// <summary>Logo file payload for upload.</summary>
    public FileUploadRequest? LogoFile { get; set; }

    [Phone, MaxLength(20)]
    public string? Phone { get; set; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    /// <summary>JSON-encoded weekly schedule, e.g. {"Monday":{"open":"09:00","close":"17:00"},...}</summary>
    public string? OpeningHours { get; set; }
}
