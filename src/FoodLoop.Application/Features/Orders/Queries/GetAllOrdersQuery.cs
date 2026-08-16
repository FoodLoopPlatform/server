using FoodLoop.Application.DTOs.Orders;
using MediatR;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Orders.Queries;

public record GetAllOrdersQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<IReadOnlyList<OrderDto>>;
