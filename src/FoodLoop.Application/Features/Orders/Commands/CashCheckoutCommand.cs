using FoodLoop.Application.DTOs.Orders;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Orders.Commands;

public record CashCheckoutCommand(Guid OrderId, Guid UserId) : IRequest<CashCheckoutResultDto>;
