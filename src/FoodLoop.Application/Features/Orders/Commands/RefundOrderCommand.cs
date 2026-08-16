using FoodLoop.Application.DTOs.Orders;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Orders.Commands;

public record RefundOrderCommand(
    Guid OrderId,
    Guid MerchantUserId,
    decimal Amount,
    string Reason) : IRequest<OrderDto>;
