using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Persistence.Repositories;

public class OrganizationRepository : Repository<Organization>, IOrganizationRepository
{
    private readonly ApplicationDbContext _db;

    public OrganizationRepository(ApplicationDbContext context) : base(context)
    {
        _db = context;
    }

    public async Task<Organization?> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(s => s.Verifications)
            .FirstOrDefaultAsync(s => s.OwnerId == ownerId, cancellationToken);

    public async Task<Organization?> GetByOwnerEmailAsync(string email, CancellationToken cancellationToken = default)
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

    public async Task<IReadOnlyList<Organization>> GetByVerificationStatusAsync(
        VerificationStatus status, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(s => s.Verifications)
            .Where(s => s.VerificationStatus == status && !s.IsDeleted)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<Organization?> GetByIdWithVerificationsAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(s => s.Verifications)
            .FirstOrDefaultAsync(s => s.Id == organizationId && !s.IsDeleted, cancellationToken);

    public async Task<Organization?> GetByIdWithReviewsAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(s => s.Reviews)
            .FirstOrDefaultAsync(s => s.Id == organizationId && !s.IsDeleted, cancellationToken);
}

