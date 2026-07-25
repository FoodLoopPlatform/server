using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodLoop.Infrastructure.Persistence.Repositories;

public class AddressRepository : Repository<Address>, IAddressRepository
{
    public AddressRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Address>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task ClearDefaultAsync(Guid userId, Guid? exceptAddressId = null, CancellationToken cancellationToken = default)
    {
        var currentDefaults = await DbSet
            .Where(a => a.UserId == userId && a.IsDefault && a.Id != exceptAddressId)
            .ToListAsync(cancellationToken);

        foreach (var address in currentDefaults)
        {
            address.IsDefault = false;
        }
    }
}
