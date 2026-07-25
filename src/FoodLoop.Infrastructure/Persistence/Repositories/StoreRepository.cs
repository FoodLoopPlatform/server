using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Persistence.Repositories;

public class StoreRepository : Repository<Store>, IStoreRepository
{
    public StoreRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Store?> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Include(s => s.Verifications)
            .FirstOrDefaultAsync(s => s.OwnerId == ownerId, cancellationToken);
}
