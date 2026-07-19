using FoodLoop.Domain.Common;

namespace FoodLoop.Domain.Entities;

public class ProductImage : BaseEntity
{
    public Guid ListingId { get; set; }
    public ProductListing? Listing { get; set; }

    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
