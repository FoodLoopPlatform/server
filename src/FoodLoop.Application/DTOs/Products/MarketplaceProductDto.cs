using System;
using System.Collections.Generic;

namespace FoodLoop.Application.DTOs.Products;

public class MarketplaceProductDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal DiscountedPrice { get; set; }
    public int QuantityAvailable { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public string ExpiryVerificationState { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? DistanceKm { get; set; }
    public IReadOnlyList<ProductImageDto> Images { get; set; } = Array.Empty<ProductImageDto>();
}
