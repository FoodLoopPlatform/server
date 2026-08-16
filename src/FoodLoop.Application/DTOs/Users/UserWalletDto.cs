using System;
using System.Collections.Generic;

namespace FoodLoop.Application.DTOs.Users;

public class UserWalletDto
{
    public decimal WalletBalance { get; set; }
    public IReadOnlyList<WalletTransactionDto> Transactions { get; set; } = new List<WalletTransactionDto>();
}

public class WalletTransactionDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
