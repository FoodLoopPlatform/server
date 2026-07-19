namespace FoodLoop.Domain.Entities;

/// <summary>Composite-key join entity: (UserId, ListingId).</summary>
public class Favorite
{
    public Guid UserId { get; set; }
    public Guid ListingId { get; set; }
    public ProductListing? Listing { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
