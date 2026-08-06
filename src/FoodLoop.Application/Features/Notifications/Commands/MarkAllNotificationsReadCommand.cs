using FoodLoop.Application.Common.Models;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Notifications.Commands;

public record MarkAllNotificationsReadCommand(Guid UserId) : IRequest<Result>;
