using FoodLoop.Domain.Common;

namespace FoodLoop.Domain.Entities;

public class Review : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid UserId { get; set; }

    public Guid StoreId { get; set; }
    public Store? Store { get; set; }

    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
}
