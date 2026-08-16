using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Features.Orders.Commands;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Orders.Commands;

public class CheckoutOrderCommandHandler : IRequestHandler<CheckoutOrderCommand, CheckoutSessionDto>
{
    private readonly ApplicationDbContext _db;
    private readonly IPaymentService _paymentService;
    private readonly IConfiguration _config;

    public CheckoutOrderCommandHandler(ApplicationDbContext db, IPaymentService paymentService, IConfiguration config)
    {
        _db = db;
        _paymentService = paymentService;
        _config = config;
    }

    public async Task<CheckoutSessionDto> Handle(CheckoutOrderCommand request, CancellationToken cancellationToken)
    {
        // 1. Fetch the order
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", request.OrderId);

        // 2. Authorization Check: verify it belongs to this customer
        if (order.UserId != request.UserId)
        {
            throw new UnauthorizedAccessException("You are not authorized to pay for this order.");
        }

        // 3. Status Check: order must be pending / unpaid
        if (order.PaymentStatus == FoodLoop.Domain.Enums.PaymentStatus.Paid)
        {
            throw new InvalidOperationException("This order has already been paid.");
        }

        // 4. Get customer user details
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);

        var names = (user.FullName ?? "Customer User").Split(' ', 2);
        var firstName = names.Length > 0 ? names[0] : "Customer";
        var lastName = names.Length > 1 ? names[1] : "User";

        // 5. Generate Payment Token from Paymob
        var paymentToken = await _paymentService.GeneratePaymentTokenAsync(
            order.Id,
            order.TotalAmount,
            user.Email ?? "customer@foodloop.com",
            firstName,
            lastName,
            user.PhoneNumber ?? "+201000000000",
            cancellationToken);

        // 6. Build direct Unified Checkout redirection link
        var publicKey = _config["Paymob:PublicKey"] ?? string.Empty;
        var baseUrl = _config["Paymob:BaseUrl"] ?? "https://accept-alpha.paymob.com";
        var checkoutUrl = $"{baseUrl}/unifiedcheckout/?publicKey={publicKey}&clientSecret={paymentToken}";

        return new CheckoutSessionDto
        {
            OrderId = order.Id,
            PaymentToken = paymentToken,
            CheckoutUrl = checkoutUrl
        };
    }
}
