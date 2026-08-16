using FoodLoop.Application.Common.Models;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Notifications.Commands;

public record RegisterDeviceTokenCommand(Guid UserId, string Token, string Platform) : IRequest<Result>;
