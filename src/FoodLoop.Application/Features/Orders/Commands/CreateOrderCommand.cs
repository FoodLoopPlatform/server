using FoodLoop.Application.DTOs.Orders;
using FoodLoop.Application.Common.Models;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Orders.Commands;

public record CheckoutItemRequest(Guid ProductId, int Quantity);

public record CreateOrderCommand(
    Guid UserId,
    List<CheckoutItemRequest> Items,
    string? IpAddress
) : IRequest<Result<OrderDto>>;
