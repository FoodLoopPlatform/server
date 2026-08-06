using FoodLoop.Domain.Common;

namespace FoodLoop.Application.Common.Interfaces;

/// <summary>
/// One SaveChanges boundary across however many repositories a service touches in a
/// request. Services depend on this instead of EF Core's DbContext directly, so
/// Application/Infrastructure stay properly separated and services are easy to unit
/// test against an in-memory fake.
///
/// Repositories are exposed as properties (Addresses, Organizations, RefreshTokens) because
/// those are the ones with bespoke queries; <see cref="Repository{TEntity}"/> is the
/// generic fallback for any other entity so future services don't need a new
/// Unit-of-Work property for every table.
/// </summary>
public interface IUnitOfWork
{
    IAddressRepository Addresses { get; }
    IOrganizationRepository Organizations { get; }
    IRefreshTokenRepository RefreshTokens { get; }

    IRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Wraps multiple repository writes (e.g. creating a user's Identity row
    /// and their draft Organization) in one DB transaction, so either both commit or neither does.</summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

