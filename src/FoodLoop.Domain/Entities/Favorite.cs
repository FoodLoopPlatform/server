using System;

namespace FoodLoop.Domain.Entities;

/// <summary>Composite-key join entity: (UserId, ProductId).</summary>
public class Favorite
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
