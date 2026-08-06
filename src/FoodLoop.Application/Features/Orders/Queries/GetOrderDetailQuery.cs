using FoodLoop.Application.DTOs.Orders;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Orders.Queries;

public record GetOrderDetailQuery(Guid OrderId, Guid UserId) : IRequest<OrderDto>;
