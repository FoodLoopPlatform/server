using FoodLoop.Application.DTOs.Listings;
using FoodLoop.Domain.Entities;
using System;
using System.Linq;

namespace FoodLoop.Infrastructure.Mappings;

internal static class ListingMappings
{
    public static ProductListingDto ToDto(this ProductListing listing) => new()
    {
        Id = listing.Id,
        StoreId = listing.StoreId,
        CategoryId = listing.CategoryId,
        CategoryName = listing.Category?.Name ?? "Unknown Category",
        Title = listing.Title,
        TitleAr = listing.TitleAr,
        Description = listing.Description,
        DescriptionAr = listing.DescriptionAr,
        OriginalPrice = listing.OriginalPrice,
        DiscountedPrice = listing.DiscountedPrice,
        QuantityAvailable = listing.QuantityAvailable,
        ExpirationDate = listing.ExpirationDate,
        Status = listing.Status.ToString(),
        Images = listing.Images != null 
            ? listing.Images.Select(i => new ProductImageDto
              {
                  Id = i.Id,
                  ImageUrl = i.ImageUrl,
                  DisplayOrder = i.DisplayOrder
              }).ToArray()
            : Array.Empty<ProductImageDto>()
    };
}
