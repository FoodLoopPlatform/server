using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Notifications.Commands;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Notifications.Commands;

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, Result>
{
    private readonly ApplicationDbContext _db;

    public MarkNotificationReadCommandHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == request.NotificationId, cancellationToken)
            ?? throw new NotFoundException("Notification", request.NotificationId);

        if (notification.UserId != request.UserId)
        {
            return Result.Fail("Unauthorized access to modify this notification.");
        }

        notification.IsRead = true;
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }
}
