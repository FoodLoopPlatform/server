using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : Repository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(ApplicationDbContext context) : base(context) { }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default) =>
        await DbSet.FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetNonRevokedByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);
}
