using FoodLoop.Application.Common.Models;
using FoodLoop.Application.Features.Notifications.Commands;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Notifications.Commands;

public class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, Result>
{
    private readonly ApplicationDbContext _db;

    public MarkAllNotificationsReadCommandHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var notifications = await _db.Notifications
            .Where(n => n.UserId == request.UserId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var n in notifications)
        {
            n.IsRead = true;
            n.ReadAt = System.DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
