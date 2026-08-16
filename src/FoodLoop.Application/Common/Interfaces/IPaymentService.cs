using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Application.Common.Interfaces;

public interface IPaymentService
{
    Task<string> GeneratePaymentTokenAsync(Guid orderId, decimal amount, string email, string firstName, string lastName, string phoneNumber, CancellationToken cancellationToken = default);
    bool VerifyHmac(string payload, string hmacReceived);
}
