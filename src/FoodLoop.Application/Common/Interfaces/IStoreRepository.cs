using FoodLoop.Domain.Entities;
using FoodLoop.Domain.Enums;

namespace FoodLoop.Application.Common.Interfaces;

public interface IStoreRepository : IRepository<Store>
{
    /// <summary>The merchant's own store, with its verification documents loaded —
    /// what StoreService needs for every onboarding-wizard step.</summary>
    Task<Store?> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>Looks up a store by the owner's email address. Used when the caller
    /// is not yet authenticated (e.g. document upload during verification onboarding).</summary>
    Task<Store?> GetByOwnerEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>All stores with a given verification status, with verifications loaded.
    /// Used by admin to list stores pending review.</summary>
    Task<IReadOnlyList<Store>> GetByVerificationStatusAsync(VerificationStatus status, CancellationToken cancellationToken = default);

    /// <summary>A single store by its own id, with verifications loaded — for admin review.</summary>
    Task<Store?> GetByIdWithVerificationsAsync(Guid storeId, CancellationToken cancellationToken = default);
}
