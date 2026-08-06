using System;

namespace FoodLoop.Application.DTOs.Admin;

public class AdminProductDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? TitleAr { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal DiscountedPrice { get; set; }
    public int QuantityAvailable { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public double? AIConfidenceScore { get; set; }
    public string? ModerationNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}


