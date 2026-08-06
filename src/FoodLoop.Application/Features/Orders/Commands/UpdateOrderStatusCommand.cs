using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Common.Models;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Orders.Commands;

public record UpdateOrderStatusCommand(
    Guid OwnerId, // Merchant owner ID
    Guid OrderId,
    string Status // Confirmed, Preparing, ReadyForPickup, Completed, Cancelled
) : IRequest<Result<OrderDto>>;
