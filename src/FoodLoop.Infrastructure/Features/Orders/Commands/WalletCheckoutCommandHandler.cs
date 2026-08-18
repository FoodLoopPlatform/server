using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Orders.Commands;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Orders.Commands;

public class WalletCheckoutCommandHandler : IRequestHandler<WalletCheckoutCommand, WalletCheckoutResultDto>
{
    private readonly ApplicationDbContext _db;

    public WalletCheckoutCommandHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<WalletCheckoutResultDto> Handle(WalletCheckoutCommand request, CancellationToken cancellationToken)
    {
        // 1. Fetch the order
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        // 2. Authorization Check
        if (order.UserId != request.UserId)
        {
            throw new ForbiddenAccessException("You are not authorized to pay for this order.");
        }

        // 3. Status Check
        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            throw new ConflictException("This order has already been paid.");
        }

        if (order.TotalAmount <= 0)
        {
            throw new ArgumentException("Order amount must be greater than zero.");
        }

        // We run the balance check and update in a transaction
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (_db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                // Fallback for EF Core InMemory database provider during testing
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
                if (user != null && user.WalletBalance >= order.TotalAmount)
                {
                    user.WalletBalance -= order.TotalAmount;
                    _db.Users.Update(user);
                }
                else
                {
                    var userExists = user != null;
                    if (!userExists)
                    {
                        throw new NotFoundException("User", request.UserId);
                    }
                    throw new ArgumentException("Insufficient wallet balance.");
                }
            }
            else
            {
                // Concurrency-safe atomic balance subtraction on SQL Server / SQLite
                var affected = await _db.Users
                    .Where(u => u.Id == request.UserId && u.WalletBalance >= order.TotalAmount)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.WalletBalance, u => u.WalletBalance - order.TotalAmount), cancellationToken);

                if (affected == 0)
                {
                    var userExists = await _db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken);
                    if (!userExists)
                    {
                        throw new NotFoundException("User", request.UserId);
                    }
                    throw new ArgumentException("Insufficient wallet balance.");
                }
            }

            // Create WalletTransaction
            var walletTx = new WalletTransaction
            {
                UserId = request.UserId,
                Amount = order.TotalAmount,
                Type = "Payment",
                ReferenceId = order.Id.ToString(),
                Description = $"Payment for Order #{order.Id.ToString()[..8].ToUpper()}"
            };
            _db.WalletTransactions.Add(walletTx);

            // Update order payment status
            order.PaymentStatus = PaymentStatus.Paid;
            order.OrderStatus = OrderStatus.Confirmed; // Auto-confirm on payment success
            order.UpdatedAt = DateTimeOffset.UtcNow;

            // Add or update Payment entity
            var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == order.Id, cancellationToken);
            if (payment == null)
            {
                payment = new Payment
                {
                    OrderId = order.Id,
                    Amount = order.TotalAmount,
                    Method = "Wallet",
                    Status = PaymentStatus.Paid,
                    TransactionReference = order.Id.ToString(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _db.Payments.Add(payment);
            }
            else
            {
                payment.Method = "Wallet";
                payment.Status = PaymentStatus.Paid;
                payment.TransactionReference = order.Id.ToString();
                payment.UpdatedAt = DateTimeOffset.UtcNow;
                _db.Payments.Update(payment);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Load final balance
            var remainingBalance = await _db.Users
                .Where(u => u.Id == request.UserId)
                .Select(u => u.WalletBalance)
                .FirstOrDefaultAsync(cancellationToken);

            return new WalletCheckoutResultDto
            {
                OrderId = order.Id,
                PaymentStatus = order.PaymentStatus.ToString(),
                OrderStatus = order.OrderStatus.ToString(),
                AmountCharged = order.TotalAmount,
                RemainingWalletBalance = remainingBalance
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
