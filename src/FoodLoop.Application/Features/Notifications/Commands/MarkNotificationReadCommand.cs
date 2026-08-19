using FoodLoop.Application.Common.Models;
using FoodLoop.Application.DTOs.Notifications;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Notifications.Commands;

public record MarkNotificationReadCommand(Guid UserId, Guid NotificationId) : IRequest<Result<NotificationDto>>;
