using MediatR;
using System;

namespace FoodLoop.Application.Features.Notifications.Queries;

public record GetUnreadNotificationsCountQuery(Guid UserId) : IRequest<int>;
