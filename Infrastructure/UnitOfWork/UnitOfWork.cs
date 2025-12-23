using System.Data.Common;
using Infrastructure.Interfaces;

namespace Infrastructure.UnitOfWork;

/// <summary>
/// پیاده‌سازی IUnitOfWork که از ITransactionContext استفاده می‌کند.
/// این کلاس یک wrapper ساده برای ITransactionScope است.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ITransactionScope _transactionScope;
    private bool _disposed = false;

    public UnitOfWork(ITransactionScope transactionScope)
    {
        _transactionScope = transactionScope ?? throw new ArgumentNullException(nameof(transactionScope));
    }

    public async Task Commit(CancellationToken cancellationToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UnitOfWork));

        await _transactionScope.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task Rollback(CancellationToken cancellationToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UnitOfWork));

        await _transactionScope.RollbackAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public DbConnection? GetConnection()
    {
        if (_disposed)
            return null;
            
        return _transactionScope.Connection;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        // ✅ استفاده از DisposeAsync برای اطمینان از rollback
        // DisposeAsync در TransactionScope خودش rollback را انجام می‌دهد
        await _transactionScope.DisposeAsync().ConfigureAwait(false);
        
        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // ✅ برای backward compatibility، DisposeAsync را به صورت synchronous صدا می‌زنیم
        // اما بهتر است از await using استفاده شود
        try
        {
            // استفاده از GetAwaiter().GetResult() برای جلوگیری از deadlock
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore errors during dispose
        }
    }
}

