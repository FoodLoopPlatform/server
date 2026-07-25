using FoodLoop.Domain.Entities;

namespace FoodLoop.Application.Common.Interfaces;

public interface IAddressRepository : IRepository<Address>
{
    /// <summary>All of a user's saved addresses, default first then newest first —
    /// matches the ordering the addresses list screen expects.</summary>
    Task<IReadOnlyList<Address>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Unsets IsDefault on every other address the user has, so setting a new
    /// default never leaves two addresses both marked default.</summary>
    Task ClearDefaultAsync(Guid userId, Guid? exceptAddressId = null, CancellationToken cancellationToken = default);
}
