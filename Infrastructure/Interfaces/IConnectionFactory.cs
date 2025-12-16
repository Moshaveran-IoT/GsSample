using System.Data.Common;

namespace Infrastructure.Interfaces;

/// <summary>
/// Interface برای ConnectionFactory که کاملاً stateless است.
/// این interface برای .NET 9 بهینه‌سازی شده و از async/await pattern استفاده می‌کند.
/// </summary>
public interface IConnectionFactory : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// ایجاد کانکشن برای عملیات خواندن (Read).
    /// همیشه کانکشن جدید می‌سازد و باز می‌کند.
    /// </summary>
    Task<DbConnection> CreateReadConnection(CancellationToken cancellationToken = default);

    /// <summary>
    /// ایجاد کانکشن برای عملیات نوشتن (Write).
    /// همیشه کانکشن جدید می‌سازد و باز می‌کند.
    /// برای استفاده در transaction مشترک، از ITransactionContext استفاده کنید.
    /// </summary>
    Task<DbConnection> CreateWriteConnection(CancellationToken cancellationToken = default);
}

