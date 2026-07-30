using FoodLoop.Domain.Common;
using System;

namespace FoodLoop.Domain.Entities;

public class ProductImage : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
