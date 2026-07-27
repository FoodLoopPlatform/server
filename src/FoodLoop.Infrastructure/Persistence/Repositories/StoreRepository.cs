using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Persistence.Repositories;

public class StoreRepository : Repository<Store>, IStoreRepository
{
    private readonly ApplicationDbContext _db;

    public StoreRepository(ApplicationDbContext context) : base(context)
    {
        _db = context;
    }

    public async Task<Store?> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(s => s.Verifications)
            .FirstOrDefaultAsync(s => s.OwnerId == ownerId, cancellationToken);

    public async Task<Store?> GetByOwnerEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToUpperInvariant();
        var owner = await _db.Users
            .Where(u => u.NormalizedEmail == normalizedEmail)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (owner == default) return null;

        return await DbSet
            .Include(s => s.Verifications)
            .FirstOrDefaultAsync(s => s.OwnerId == owner, cancellationToken);
    }
}
