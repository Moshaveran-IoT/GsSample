using System.Data.Common;

namespace Infrastructure.Interfaces;

/// <summary>
/// Interface برای مدیریت transaction مشترک بین چند Repository.
/// 
/// این interface مشکل "نیاز به transaction مشترک" را حل می‌کند
/// بدون اینکه ConnectionFactory را stateful کنیم.
/// 
/// استفاده:
/// 1. در Handler یا Service، یک TransactionContext ایجاد کنید
/// 2. از BeginTransactionAsync() برای شروع transaction استفاده کنید
/// 3. Repositoryها از GetConnection() برای دریافت همان connection استفاده می‌کنند
/// 4. در پایان، CommitAsync() یا RollbackAsync() را صدا بزنید
/// 5. TransactionContext را dispose کنید
/// 
/// مثال:
/// await using var transaction = await _transactionContext.BeginTransactionAsync(ct);
/// await _repo1.SaveAsync(data1, ct);
/// await _repo2.SaveAsync(data2, ct);
/// await transaction.CommitAsync(ct);
/// </summary>
public interface ITransactionContext
{
    /// <summary>
    /// شروع یک transaction جدید.
    /// این متد یک connection می‌سازد و transaction را شروع می‌کند.
    /// این connection در AsyncLocal ذخیره می‌شود تا Repositoryها بتوانند از آن استفاده کنند.
    /// </summary>
    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// دریافت connection فعلی که در transaction scope است.
    /// اگر transaction شروع نشده باشد، null برمی‌گرداند.
    /// Repositoryها باید از این متد استفاده کنند تا connection مشترک را دریافت کنند.
    /// </summary>
    DbConnection? GetCurrentConnection();

    /// <summary>
    /// دریافت transaction فعلی که در scope است.
    /// اگر transaction شروع نشده باشد، null برمی‌گرداند.
    /// </summary>
    DbTransaction? GetCurrentTransaction();
}

/// <summary>
/// Scope یک transaction که connection و transaction را مدیریت می‌کند.
/// </summary>
public interface ITransactionScope : IAsyncDisposable
{
    /// <summary>
    /// Connection مربوط به این transaction.
    /// </summary>
    DbConnection Connection { get; }

    /// <summary>
    /// Transaction مربوط به این scope.
    /// </summary>
    DbTransaction Transaction { get; }

    /// <summary>
    /// Commit کردن transaction.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback کردن transaction.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

