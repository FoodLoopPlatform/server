namespace FoodLoop.Domain.Entities;

public class OrderItem
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid ListingId { get; set; }
    public ProductListing? Listing { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
