using System;
using System.Collections.Generic;

namespace FoodLoop.Application.DTOs.Orders;

public class DeliveryFleetDto
{
    public int TotalActiveOrders { get; set; }
    public int PendingCount { get; set; }
    public int PreparingCount { get; set; }
    public int ReadyForPickupCount { get; set; }
    public List<FleetOrderDto> Orders { get; set; } = new();
}

public class FleetOrderDto
{
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public DateTimeOffset? LastUpdatedAt { get; set; }
}
