using System;

namespace FoodLoop.Application.DTOs.Orders;

public class CashCheckoutResultDto
{
    public Guid OrderId { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "Cash";
    public decimal AmountDue { get; set; }
    public string Message { get; set; } = "Order confirmed. Please pay in cash upon pickup or delivery.";
}
