using System;

namespace FoodLoop.Application.DTOs.Products;

public class PriceHistoryDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public decimal OldOriginalPrice { get; set; }
    public decimal OldDiscountedPrice { get; set; }
    public decimal NewOriginalPrice { get; set; }
    public decimal NewDiscountedPrice { get; set; }
    public string? ChangeReason { get; set; }
    public Guid ChangedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
