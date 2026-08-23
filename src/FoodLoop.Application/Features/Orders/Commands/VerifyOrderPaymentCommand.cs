using FoodLoop.Application.DTOs.Orders;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Orders.Commands;

public record VerifyOrderPaymentCommand(Guid OrderId, Guid UserId, string? TransactionId = null) : IRequest<OrderDto>;
