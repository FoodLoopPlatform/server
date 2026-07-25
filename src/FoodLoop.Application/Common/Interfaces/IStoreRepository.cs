using FoodLoop.Domain.Entities;

namespace FoodLoop.Application.Common.Interfaces;

public interface IStoreRepository : IRepository<Store>
{
    /// <summary>The merchant's own store, with its verification documents loaded —
    /// what StoreService needs for every onboarding-wizard step.</summary>
    Task<Store?> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default);
}
