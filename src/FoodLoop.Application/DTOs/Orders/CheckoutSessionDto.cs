using System;

namespace FoodLoop.Application.DTOs.Orders;

public class CheckoutSessionDto
{
    public Guid OrderId { get; set; }
    public string PaymentToken { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
}
