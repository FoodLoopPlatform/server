using FoodLoop.Domain.Common;
using System;

namespace FoodLoop.Domain.Entities;

public class WalletTransaction : BaseEntity
{
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty; // "Refund", "Payment", etc.
    public string? ReferenceId { get; set; }
    public string? Description { get; set; }
}
