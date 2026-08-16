using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Entities;
using FoodLoop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoodLoop.Infrastructure.Services;

public class UserDeviceTokenService : IUserDeviceTokenService
{
    private readonly ApplicationDbContext _db;

    public UserDeviceTokenService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task UpsertAsync(Guid userId, string token, string platform, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var existing = await _db.UserDeviceTokens
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Token == token, cancellationToken);

        if (existing is null)
        {
            _db.UserDeviceTokens.Add(new UserDeviceToken
            {
                UserId = userId,
                Token = token,
                Platform = string.IsNullOrWhiteSpace(platform) ? "Mobile" : platform,
                IsActive = true
            });
        }
        else
        {
            existing.Platform = string.IsNullOrWhiteSpace(platform) ? "Mobile" : platform;
            existing.IsActive = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetActiveTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserDeviceTokens
            .Where(x => x.UserId == userId && x.IsActive)
            .Select(x => x.Token)
            .ToListAsync(cancellationToken);
    }
}
