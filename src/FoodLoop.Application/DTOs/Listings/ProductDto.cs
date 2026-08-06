using System;
using System.Collections.Generic;

namespace FoodLoop.Application.DTOs.Listings;

public class ProductDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? TitleAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal DiscountedPrice { get; set; }
    public int QuantityAvailable { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public IReadOnlyList<ProductImageDto> Images { get; set; } = Array.Empty<ProductImageDto>();
}

public class ProductImageDto
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

