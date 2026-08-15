using MediatR;
using FoodLoop.Application.DTOs.Orders;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Orders.Queries;

/// <summary>GET /stores/me/delivery/fleet — active orders with delivery status overview.</summary>
public record GetDeliveryFleetQuery(Guid OwnerId) : IRequest<DeliveryFleetDto>;
