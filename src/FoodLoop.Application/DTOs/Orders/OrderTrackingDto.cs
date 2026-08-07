using System;
using System.Collections.Generic;

namespace FoodLoop.Application.DTOs.Orders;

public class OrderTrackingDto
{
    public Guid OrderId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public DateTimeOffset? LastUpdatedAt { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string? StoreLogo { get; set; }
    public List<TrackingStepDto> Steps { get; set; } = new();
    public List<OrderTrackingItemDto> Items { get; set; } = new();
}

public class TrackingStepDto
{
    public string Status { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class OrderTrackingItemDto
{
    public string ProductTitle { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
