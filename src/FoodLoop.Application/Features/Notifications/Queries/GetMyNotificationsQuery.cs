using FoodLoop.Application.DTOs.Notifications;
using MediatR;
using System;
using System.Collections.Generic;

namespace FoodLoop.Application.Features.Notifications.Queries;

public record GetMyNotificationsQuery(
    Guid UserId, 
    int PageNumber = 1, 
    int PageSize = 20, 
    bool? IsRead = null) : IRequest<IReadOnlyList<NotificationDto>>;
