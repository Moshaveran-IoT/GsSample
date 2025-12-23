using System.Data.Common;
using Infrastructure.Interfaces;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Factories;

/// <summary>
/// پیاده‌سازی ITransactionContext با استفاده از AsyncLocal و ExecutionContext.
/// 
/// این کلاس connection و transaction را در AsyncLocal ذخیره می‌کند
/// تا Repositoryها در همان async context بتوانند از connection مشترک استفاده کنند.
/// 
/// مزایا:
/// - Thread-safe: هر async context connection خودش را دارد
/// - Async/await compatible: کاملاً async است
/// - No deadlock: از AsyncLocal استفاده می‌کند نه lock
/// - Scoped: connection فقط در scope transaction زنده است
/// - ExecutionContext flow: از ExecutionContext برای بهبود propagation استفاده می‌کند
/// </summary>
public sealed class TransactionContext : ITransactionContext
{
    // ✅ استفاده از AsyncLocal برای ذخیره transaction scope
    // AsyncLocal به صورت خودکار در async context propagate می‌شود
    // با استفاده از ConfigureAwait(true) در BeginTransactionAsync، 
    // async context حفظ می‌شود و AsyncLocal درست کار می‌کند
    private static readonly AsyncLocal<TransactionScope?> _currentScope = new();

    private readonly IConnectionFactory _connectionFactory;

    public TransactionContext(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// شروع یک transaction جدید.
    /// 
    /// این متد:
    /// 1. یک Connection جدید از ConnectionFactory می‌سازد
    /// 2. یک Transaction روی آن Connection شروع می‌کند
    /// 3. TransactionScope را ایجاد می‌کند و در AsyncLocal ذخیره می‌کند
    /// 4. Repositoryها می‌توانند از GetCurrentConnection() برای دریافت همان Connection استفاده کنند
    /// 
    /// مالکیت Connection: TransactionScope مالک Connection است و فقط در DisposeAsync آن را dispose می‌کند.
    /// </summary>
    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
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
        // ✅ استفاده از ConfigureAwait(true) برای حفظ async context
        var connection = await _connectionFactory.CreateWriteConnection(cancellationToken)
            .ConfigureAwait(true);

        // ✅ شروع transaction با isolation level ReadCommitted
        // این isolation level از dirty reads جلوگیری می‌کند و rollback را تضمین می‌کند
        // ReadCommitted تضمین می‌کند که داده‌های uncommitted قابل خواندن نیستند
        var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted,
            cancellationToken)
            .ConfigureAwait(true);

        // ایجاد scope و ذخیره در AsyncLocal
        // ✅ AsyncLocal به صورت خودکار در async context propagate می‌شود
        // با استفاده از ConfigureAwait(true) در بالا، async context حفظ می‌شود
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

            // ✅ Rollback transaction
            await Transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _rolledBack = true;
            
            // ✅ اطمینان از اینکه rollback کامل شده است
            // کمی صبر می‌کنیم تا rollback کامل شود
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            // ✅ اگر commit یا rollback نشده باشد، rollback می‌کنیم
            // این باید قبل از ClearScope انجام شود تا rollback درست کار کند
            if (!_committed && !_rolledBack)
            {
                try
                {
                    // ✅ Rollback قبل از ClearScope
                    // استفاده از ConfigureAwait(false) برای جلوگیری از deadlock
                    await Transaction.RollbackAsync().ConfigureAwait(false);
                    _rolledBack = true; // ✅ علامت‌گذاری rollback
                    
                    // ✅ اطمینان از اینکه rollback کامل شده است
                    // کمی صبر می‌کنیم تا rollback کامل شود
                    await Task.Delay(100).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore errors during rollback in dispose
                    // اما باز هم _rolledBack را set می‌کنیم تا از rollback دوباره جلوگیری کنیم
                    _rolledBack = true;
                }
            }

            // ✅ Dispose کردن transaction و connection
            // Transaction باید قبل از Connection dispose شود
            // این باید قبل از ClearScope انجام شود تا rollback درست کار کند
            try
            {
                if (Transaction != null)
                {
                    await Transaction.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                // Ignore errors during transaction dispose
            }
            
            try
            {
                if (Connection != null)
                {
                    await Connection.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                // Ignore errors during connection dispose
            }
            
            // ✅ پاک کردن scope از AsyncLocal (بعد از dispose)
            // این باید بعد از dispose کردن transaction و connection انجام شود
            _context.ClearScope();

            _disposed = true;
        }
    }
}

