using FoodLoop.Application.Common.Models;
using System.ComponentModel.DataAnnotations;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.DTOs.Organizations;

/// <summary>PATCH /organizations/me â€” all fields optional; only supplied fields are updated.</summary>
public class UpdateOrganizationProfileRequest
{
    [MaxLength(150)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public BusinessCategory? BusinessCategory { get; set; }

    /// <summary>URL of the organization logo image.</summary>
    public string? Logo { get; set; }

    /// <summary>Logo file payload for upload.</summary>
    public FileUploadRequest? LogoFile { get; set; }

    /// <summary>URL of the organization cover photo image.</summary>
    public string? CoverPhoto { get; set; }

    /// <summary>Cover photo file payload for upload.</summary>
    public FileUploadRequest? CoverPhotoFile { get; set; }

    [Phone, MaxLength(20)]
    public string? Phone { get; set; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    /// <summary>JSON-encoded weekly schedule, e.g. {"Monday":{"open":"09:00","close":"17:00"},...}</summary>
    public string? OpeningHours { get; set; }
}

