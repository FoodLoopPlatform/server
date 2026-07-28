using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
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
        var ownerId = await _db.Users
            .Where(u => u.NormalizedEmail == normalizedEmail)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerId == default) return null;

        return await DbSet
            .Include(s => s.Verifications)
            .FirstOrDefaultAsync(s => s.OwnerId == ownerId, cancellationToken);
    }

    public async Task<IReadOnlyList<Store>> GetByVerificationStatusAsync(
        VerificationStatus status, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(s => s.Verifications)
            .Where(s => s.VerificationStatus == status && !s.IsDeleted)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<Store?> GetByIdWithVerificationsAsync(
        Guid storeId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(s => s.Verifications)
            .FirstOrDefaultAsync(s => s.Id == storeId && !s.IsDeleted, cancellationToken);
}
