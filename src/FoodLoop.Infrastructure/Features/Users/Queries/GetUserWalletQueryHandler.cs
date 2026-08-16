using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.DTOs.Users;
using FoodLoop.Application.Features.Users.Queries;
using FoodLoop.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Features.Users.Queries;

public class GetUserWalletQueryHandler : IRequestHandler<GetUserWalletQuery, UserWalletDto>
{
    private readonly ApplicationDbContext _db;

    public GetUserWalletQueryHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UserWalletDto> Handle(GetUserWalletQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);

        var transactions = await _db.WalletTransactions
            .AsNoTracking()
            .Where(t => t.UserId == request.UserId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new WalletTransactionDto
            {
                Id = t.Id,
                Amount = t.Amount,
                Type = t.Type,
                ReferenceId = t.ReferenceId,
                Description = t.Description,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new UserWalletDto
        {
            WalletBalance = user.WalletBalance,
            Transactions = transactions
        };
    }
}
