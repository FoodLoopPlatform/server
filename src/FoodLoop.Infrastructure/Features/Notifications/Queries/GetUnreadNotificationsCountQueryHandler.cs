using FoodLoop.Application.Features.Notifications.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Notifications.Queries;

public class GetUnreadNotificationsCountQueryHandler : IRequestHandler<GetUnreadNotificationsCountQuery, int>
{
    private readonly ApplicationDbContext _db;

    public GetUnreadNotificationsCountQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<int> Handle(GetUnreadNotificationsCountQuery request, CancellationToken cancellationToken)
    {
        return await _db.Notifications
            .Where(n => n.UserId == request.UserId && !n.IsRead)
            .CountAsync(cancellationToken);
    }
}
