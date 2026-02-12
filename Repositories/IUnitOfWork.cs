namespace PrintVault3D.Repositories;

/// <summary>
/// Unit of Work pattern for managing transactions across multiple repositories.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    ICategoryRepository Categories { get; }
    IModel3DRepository Models { get; }
    IGcodeRepository Gcodes { get; }
    ICollectionRepository Collections { get; }

    /// <summary>
    /// Saves all changes made in this unit of work to the database.
    /// </summary>
    Task<int> SaveChangesAsync();

    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    Task BeginTransactionAsync();

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    Task CommitTransactionAsync();

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    Task RollbackTransactionAsync();
}

