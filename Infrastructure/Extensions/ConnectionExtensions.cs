using System.Data.Common;
using Dapper;
using Infrastructure.Interfaces;

namespace Infrastructure.Extensions;

/// <summary>
/// Extension methods برای ساده‌سازی استفاده از ConnectionFactory و TransactionContext
/// در Repositoryها برای Read/Write Separation.
/// </summary>
public static class ConnectionExtensions
{
    /// <summary>
    /// دریافت connection مناسب برای عملیات Read.
    /// 
    /// الگوی استفاده:
    /// - اگر transaction فعال باشد: از Write DB استفاده می‌کند (برای consistency)
    /// - در غیر این صورت: از Read Replica استفاده می‌کند
    /// 
    /// مثال:
    /// await using var db = await GetReadConnectionAsync(_connectionFactory, _transactionContext, ct);
    /// var result = await db.QueryAsync&lt;Person&gt;("SELECT * FROM Persons", cancellationToken: ct);
    /// </summary>
    public static async Task<DbConnection> GetReadConnectionAsync(
        IConnectionFactory connectionFactory,
        ITransactionContext transactionContext,
        CancellationToken cancellationToken = default)
    {
        // ✅ اگر transaction فعال باشد، از Write DB استفاده می‌کنیم (برای consistency)
        // نکته مهم: این متد فقط reference به Connection را برمی‌گرداند (نه ownership)
        // Connection در TransactionScope نگهداری می‌شود و فقط در DisposeAsync dispose می‌شود
        var currentConnection = transactionContext.GetCurrentConnection();
        if (currentConnection != null)
        {
            return currentConnection;
        }

        // ✅ در غیر این صورت، از Read Replica استفاده می‌کنیم
        return await connectionFactory.CreateReadConnection(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// دریافت connection مناسب برای عملیات Write.
    /// 
    /// الگوی استفاده:
    /// - اگر transaction فعال باشد: از connection مشترک استفاده می‌کند
    /// - در غیر این صورت: connection جدید از Write DB می‌سازد
    /// 
    /// مثال:
    /// await using var db = await GetWriteConnectionAsync(_connectionFactory, _transactionContext, ct);
    /// await db.ExecuteAsync("INSERT INTO Persons ...", person, cancellationToken: ct);
    /// 
    /// نکته مهم: اگر Transaction فعال باشد، این متد همان Connection را برمی‌گرداند
    /// و آن را dispose نمی‌کند. Connection فقط در TransactionScope.DisposeAsync dispose می‌شود.
    /// </summary>
    public static async Task<DbConnection> GetWriteConnectionAsync(
        IConnectionFactory connectionFactory,
        ITransactionContext transactionContext,
        CancellationToken cancellationToken = default)
    {
        // ✅ اگر transaction فعال باشد، از connection مشترک استفاده می‌کنیم
        var currentConnection = transactionContext.GetCurrentConnection();
        if (currentConnection != null)
        {
            return currentConnection;
        }

        // ✅ در غیر این صورت، connection جدید از Write DB می‌سازیم
        return await connectionFactory.CreateWriteConnection(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// اجرای یک query با استفاده از Read Connection.
    /// 
    /// این متد به صورت خودکار connection مناسب را انتخاب می‌کند:
    /// - اگر transaction فعال باشد: از Write DB استفاده می‌کند
    /// - در غیر این صورت: از Read Replica استفاده می‌کند
    /// 
    /// مثال:
    /// var persons = await ExecuteReadQueryAsync(
    ///     _connectionFactory, 
    ///     _transactionContext, 
    ///     async db => await db.QueryAsync&lt;Person&gt;("SELECT * FROM Persons", cancellationToken: ct),
    ///     ct);
    /// </summary>
    public static async Task<T> ExecuteReadQueryAsync<T>(
        IConnectionFactory connectionFactory,
        ITransactionContext transactionContext,
        Func<DbConnection, CancellationToken, Task<T>> query,
        CancellationToken cancellationToken = default)
    {
        var currentConnection = transactionContext.GetCurrentConnection();
        
        if (currentConnection != null)
        {
            // ✅ استفاده از connection مشترک (transaction فعال است)
            return await query(currentConnection, cancellationToken).ConfigureAwait(false);
        }

        // ✅ استفاده از Read Replica
        await using var db = await connectionFactory.CreateReadConnection(cancellationToken).ConfigureAwait(false);
        return await query(db, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// اجرای یک command با استفاده از Write Connection.
    /// 
    /// این متد به صورت خودکار connection مناسب را انتخاب می‌کند:
    /// - اگر transaction فعال باشد: از connection مشترک استفاده می‌کند
    /// - در غیر این صورت: connection جدید از Write DB می‌سازد
    /// 
    /// مثال:
    /// var rowsAffected = await ExecuteWriteCommandAsync(
    ///     _connectionFactory, 
    ///     _transactionContext, 
    ///     async db => await db.ExecuteAsync("INSERT INTO Persons ...", person, cancellationToken: ct),
    ///     ct);
    /// </summary>
    public static async Task<T> ExecuteWriteCommandAsync<T>(
        IConnectionFactory connectionFactory,
        ITransactionContext transactionContext,
        Func<DbConnection, CancellationToken, Task<T>> command,
        CancellationToken cancellationToken = default)
    {
        var currentConnection = transactionContext.GetCurrentConnection();
        
        if (currentConnection != null)
        {
            // ✅ استفاده از connection مشترک (transaction فعال است)
            return await command(currentConnection, cancellationToken).ConfigureAwait(false);
        }

        // ✅ استفاده از Write DB
        await using var db = await connectionFactory.CreateWriteConnection(cancellationToken).ConfigureAwait(false);
        return await command(db, cancellationToken).ConfigureAwait(false);
    }

    public static async Task ExecuteWriteCommandAsync(
        IConnectionFactory connectionFactory,
        ITransactionContext transactionContext,
        Func<DbConnection, CancellationToken, Task> command,
        CancellationToken cancellationToken = default)
    {
        var currentConnection = transactionContext.GetCurrentConnection();

        if (currentConnection != null)
        {
            // ✅ استفاده از connection مشترک (transaction فعال است)
            await command(currentConnection, cancellationToken).ConfigureAwait(false);
            return;
        }

        // ✅ استفاده از Write DB
        await using var db = await connectionFactory.CreateWriteConnection(cancellationToken).ConfigureAwait(false);
        await command(db, cancellationToken).ConfigureAwait(false);
        return;
    }
}

