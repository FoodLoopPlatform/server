using FoodLoop.Application.DTOs.Products;
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
        Description = product.Description,
        OriginalPrice = product.OriginalPrice,
        DiscountedPrice = product.DiscountedPrice,
        QuantityAvailable = product.QuantityAvailable,
        ExpirationDate = product.ExpirationDate,
        ExpiryVerificationState = product.ExpiryVerificationState ?? "Manual",
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


