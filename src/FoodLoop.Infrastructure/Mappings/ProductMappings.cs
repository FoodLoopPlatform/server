using FoodLoop.Application.DTOs.Listings;
using FoodLoop.Domain.Entities;
using System;
using System.Linq;

namespace FoodLoop.Infrastructure.Mappings;

internal static class ProductMappings
{
    public static ProductDto ToDto(this Product product) => new()
    {
        Id = product.Id,
        OrganizationId = product.OrganizationId,
        CategoryId = product.CategoryId,
        CategoryName = product.Category?.Name ?? "Unknown Category",
        Title = product.Title,
        TitleAr = product.TitleAr,
        Description = product.Description,
        DescriptionAr = product.DescriptionAr,
        OriginalPrice = product.OriginalPrice,
        DiscountedPrice = product.DiscountedPrice,
        QuantityAvailable = product.QuantityAvailable,
        ExpirationDate = product.ExpirationDate,
        Status = product.Status.ToString(),
        Images = product.Images != null 
            ? product.Images.Select(i => new ProductImageDto
              {
                  Id = i.Id,
                  ImageUrl = i.ImageUrl,
                  DisplayOrder = i.DisplayOrder
              }).ToArray()
            : Array.Empty<ProductImageDto>()
    };
}

