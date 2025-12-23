using System.Data.Common;

namespace Infrastructure.UnitOfWork;

/// <summary>
/// Interface برای Unit of Work pattern.
/// این interface یک wrapper ساده برای ITransactionScope است.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Commit کردن transaction.
    /// </summary>
    Task Commit(CancellationToken cancellationToken);
    
    /// <summary>
    /// Rollback کردن transaction.
    /// </summary>
    Task Rollback(CancellationToken cancellationToken);
    
    /// <summary>
    /// دریافت connection فعلی که در transaction scope است.
    /// این برای تست‌ها و debugging مفید است.
    /// </summary>
    DbConnection? GetConnection();
}

/// <summary>
/// Interface برای ایجاد Unit of Work.
/// </summary>
public interface IUnitOfWorkManager
{
    /// <summary>
    /// ایجاد یک Unit of Work جدید.
    /// </summary>
    Task<IUnitOfWork> CreateNew(CancellationToken cancellationToken);
}

