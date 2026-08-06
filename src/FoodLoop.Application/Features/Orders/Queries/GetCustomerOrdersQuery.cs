using FoodLoop.Application.DTOs.Orders;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Orders.Queries;

public record GetCustomerOrdersQuery(Guid UserId) : IRequest<IReadOnlyList<OrderDto>>;
