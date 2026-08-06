using FoodLoop.Domain.Common;
using FoodLoop.Domain.Enums;
using System;
using System.Collections.Generic;

namespace FoodLoop.Domain.Entities;

public class Product : BaseEntity, ISoftDelete
{
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? TitleAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }

    public decimal OriginalPrice { get; set; }
    public decimal DiscountedPrice { get; set; }
    public int QuantityAvailable { get; set; }

    public DateOnly ExpirationDate { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Active;
    public string? ModerationNote { get; set; }

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public AIRecognitionResult? AIRecognitionResult { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public bool IsDeleted { get; set; }
}
