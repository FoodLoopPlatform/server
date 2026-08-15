using MediatR;
using FoodLoop.Application.DTOs.Orders;
using System;

namespace FoodLoop.Application.Features.Orders.Queries;

/// <summary>GET /orders/{id}/tracking — customer-facing real-time order status.</summary>
public record GetOrderTrackingQuery(Guid OrderId, Guid UserId) : IRequest<OrderTrackingDto>;
