using FoodLoop.Application.DTOs.Notifications;
using MediatR;
using System;

namespace FoodLoop.Application.Features.Notifications.Queries;

public record GetNotificationByIdQuery(Guid UserId, Guid NotificationId) : IRequest<NotificationDto>;
