using System.Data.Common;
using Infrastructure.Interfaces;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Factories;

/// <summary>
/// پیاده‌سازی ITransactionContext با استفاده از AsyncLocal.
/// 
/// این کلاس connection و transaction را در AsyncLocal ذخیره می‌کند
/// تا Repositoryها در همان async context بتوانند از connection مشترک استفاده کنند.
/// 
/// مزایا:
/// - Thread-safe: هر async context connection خودش را دارد
/// - Async/await compatible: کاملاً async است
/// - No deadlock: از AsyncLocal استفاده می‌کند نه lock
/// - Scoped: connection فقط در scope transaction زنده است
/// </summary>
public sealed class TransactionContext : ITransactionContext
{
    private static readonly AsyncLocal<TransactionScope?> _currentScope = new();

    private readonly IConnectionFactory _connectionFactory;

    public TransactionContext(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// شروع یک transaction جدید.
    /// </summary>
    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default) // Nowhere used. After using, how/who sends Connection and Transaction to multiple repos?
    {
        // اگر قبلاً یک transaction در این context شروع شده باشد، خطا می‌دهیم
        if (_currentScope.Value != null)
        {
            throw new InvalidOperationException(
                "A transaction is already active in this async context. " +
                "Nested transactions are not supported. " +
                "Please commit or rollback the current transaction first.");
        }

        // ایجاد connection جدید
        var connection = await _connectionFactory.CreateWriteConnection(cancellationToken)
            .ConfigureAwait(false);

        // شروع transaction
        var transaction = await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // ایجاد scope و ذخیره در AsyncLocal
        var scope = new TransactionScope(connection, transaction, this);
        _currentScope.Value = scope;

        return scope;
    }

    /// <summary>
    /// دریافت connection فعلی که در transaction scope است.
    /// </summary>
    public DbConnection? GetCurrentConnection()
    {
        return _currentScope.Value?.Connection;
    }

    /// <summary>
    /// دریافت transaction فعلی که در scope است.
    /// </summary>
    public DbTransaction? GetCurrentTransaction()
    {
        return _currentScope.Value?.Transaction;
    }

    /// <summary>
    /// پاک کردن scope فعلی (برای استفاده داخلی).
    /// </summary>
    internal void ClearScope()
    {
        _currentScope.Value = null;
    }

    /// <summary>
    /// پیاده‌سازی ITransactionScope.
    /// </summary>
    private sealed class TransactionScope : ITransactionScope
    {
        private readonly TransactionContext _context;
        private bool _disposed = false;
        private bool _committed = false;
        private bool _rolledBack = false;

        public DbConnection Connection { get; }
        public DbTransaction Transaction { get; }

        public TransactionScope(DbConnection connection, DbTransaction transaction, TransactionContext context)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TransactionScope));

            if (_committed)
                throw new InvalidOperationException("Transaction has already been committed.");

            if (_rolledBack)
                throw new InvalidOperationException("Transaction has already been rolled back.");

            await Transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _committed = true;
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TransactionScope));

            if (_committed)
                throw new InvalidOperationException("Transaction has already been committed.");

            if (_rolledBack)
                return; // Idempotent

            await Transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _rolledBack = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            // اگر commit یا rollback نشده باشد، rollback می‌کنیم
            if (!_committed && !_rolledBack)
            {
                try
                {
                    await Transaction.RollbackAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Ignore errors during rollback in dispose
                }
            }

            // پاک کردن scope از AsyncLocal
            _context.ClearScope();

            // Dispose کردن transaction و connection
            await Transaction.DisposeAsync().ConfigureAwait(false);
            await Connection.DisposeAsync().ConfigureAwait(false);

            _disposed = true;
        }
    }
}

