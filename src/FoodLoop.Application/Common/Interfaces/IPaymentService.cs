using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Application.Common.Interfaces;

public class PaymobTransactionDetailsDto
{
    public string TransactionId { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public long AmountCents { get; set; }
    public string? SpecialReference { get; set; }
}

public interface IPaymentService
{
    Task<string> GeneratePaymentTokenAsync(Guid orderId, decimal amount, string email, string firstName, string lastName, string phoneNumber, CancellationToken cancellationToken = default);
    bool VerifyHmac(string payload, string hmacReceived);
    Task<PaymobTransactionDetailsDto?> GetTransactionDetailsAsync(string transactionId, CancellationToken cancellationToken = default);
}

