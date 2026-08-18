using System;

namespace FoodLoop.Application.DTOs.Orders;

public class WalletCheckoutResultDto
{
    public Guid OrderId { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public decimal AmountCharged { get; set; }
    public decimal RemainingWalletBalance { get; set; }
}
