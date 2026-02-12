using Microsoft.EntityFrameworkCore.Storage;
using PrintVault3D.Data;

namespace PrintVault3D.Repositories;

/// <summary>
/// Unit of Work implementation for coordinating repository operations.
/// </summary>
public class UnitOfWork : IUnitOfWork, IAsyncDisposable
{
    private readonly PrintVaultDbContext _context;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    private ICategoryRepository? _categories;
    private IModel3DRepository? _models;
    private IGcodeRepository? _gcodes;
    private ICollectionRepository? _collections;

    public UnitOfWork(PrintVaultDbContext context)
    {
        _context = context;
    }

    public ICategoryRepository Categories => 
        _categories ??= new CategoryRepository(_context);

    public IModel3DRepository Models => 
        _models ??= new Model3DRepository(_context);

    public IGcodeRepository Gcodes => 
        _gcodes ??= new GcodeRepository(_context);

    public ICollectionRepository Collections => 
        _collections ??= new CollectionRepository(_context);

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        // Only dispose the transaction, NOT the DbContext
        // The DbContext lifetime is managed by the DI container
        _transaction?.Dispose();
        _transaction = null;
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        
        // Async dispose the transaction if present
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }
}

