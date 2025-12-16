using System.Data.Common;
using Infrastructure.Interfaces;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Factories;

/// <summary>
/// Factory برای ایجاد و مدیریت کانکشن‌های SQL.
/// این کلاس کاملاً stateless است و همیشه کانکشن جدید می‌سازد.
/// 
/// مزایا:
/// - بدون cache: جلوگیری از race condition و deadlock
/// - کاملاً async: استفاده از async/await pattern
/// - آماده برای Read Replicas: امکان load balancing بین replicas
/// - Thread-safe: هر thread کانکشن خودش را می‌گیرد
/// 
/// Connection reuse توسط SQL Server Connection Pooling انجام می‌شود.
/// برای transaction مشترک بین چند Repository، از ITransactionContext استفاده کنید.
/// </summary>
public sealed class ConnectionFactory(string writeConnectionString, string readConnectionString) 
    : IConnectionFactory, IAsyncDisposable
{
    private readonly string _writeConnectionString = writeConnectionString 
        ?? throw new ArgumentNullException(nameof(writeConnectionString));
    private readonly string _readConnectionString = readConnectionString 
        ?? throw new ArgumentNullException(nameof(readConnectionString));

    /// <summary>
    /// ایجاد کانکشن برای عملیات خواندن (Read).
    /// همیشه کانکشن جدید می‌سازد و باز می‌کند.
    /// مصرف‌کننده (Repository) باید با await using آن را dispose کند.
    /// </summary>
    public async Task<DbConnection> CreateReadConnection(CancellationToken cancellationToken = default)
    {
        var conn = new SqlConnection(_readConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return conn;
    }

    /// <summary>
    /// ایجاد کانکشن برای عملیات نوشتن (Write).
    /// همیشه کانکشن جدید می‌سازد و باز می‌کند.
    /// مصرف‌کننده (Repository) باید با await using آن را dispose کند.
    /// 
    /// برای transaction مشترک بین چند Repository:
    /// از ITransactionContext.BeginTransactionAsync() استفاده کنید.
    /// </summary>
    public async Task<DbConnection> CreateWriteConnection(CancellationToken cancellationToken = default)
    {
        var conn = new SqlConnection(_writeConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return conn;
    }

    /// <summary>
    /// Dispose برای IDisposable interface.
    /// چون این کلاس stateless است، چیزی برای dispose کردن ندارد.
    /// </summary>
    public void Dispose()
    {
        // Stateless - nothing to dispose
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// DisposeAsync برای IAsyncDisposable interface.
    /// چون این کلاس stateless است، چیزی برای dispose کردن ندارد.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        // Stateless - nothing to dispose
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}

