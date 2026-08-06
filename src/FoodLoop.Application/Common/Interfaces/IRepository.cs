using System.Linq.Expressions;
using FoodLoop.Domain.Common;

namespace FoodLoop.Application.Common.Interfaces;

/// <summary>
/// Generic data-access abstraction over a single entity type. Covers the common CRUD
/// shape; entities that need bespoke queries (Address, Organization, RefreshToken) get a
/// dedicated repository interface that extends this one instead of piling every
/// possible query onto here.
///
/// <see cref="Query"/> is the deliberate escape hatch: it returns IQueryable so a
/// service can compose Include/Where/Select/paging without the repository needing a
/// method for every shape a caller might want. This keeps the repository from turning
/// into a second, ad-hoc ORM while still hiding DbContext/DbSet from Application code.
/// </summary>
public interface IRepository<TEntity> where TEntity : BaseEntity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TEntity?> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>Composable IQueryable for cases FindAsync/SingleOrDefaultAsync don't cover
    /// (Include, projection, paging). Executes lazily, same as EF's own DbSet.</summary>
    IQueryable<TEntity> Query();

    void Add(TEntity entity);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}

