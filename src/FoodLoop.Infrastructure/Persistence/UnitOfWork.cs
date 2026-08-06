using System.Collections.Concurrent;
using FoodLoop.Application.Common.Interfaces;
using FoodLoop.Domain.Common;
using FoodLoop.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace FoodLoop.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _transaction;

    private IAddressRepository? _addresses;
    private IOrganizationRepository? _organizations;
    private IRefreshTokenRepository? _refreshTokens;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public IAddressRepository Addresses => _addresses ??= new AddressRepository(_context);
    public IOrganizationRepository Organizations => _organizations ??= new OrganizationRepository(_context);
    public IRefreshTokenRepository RefreshTokens => _refreshTokens ??= new RefreshTokenRepository(_context);

    public IRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
    {
        return (IRepository<TEntity>)_repositories.GetOrAdd(
            typeof(TEntity),
            _ => new Repository<TEntity>(_context));
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null) return;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null) return;

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        GC.SuppressFinalize(this);
    }
}


