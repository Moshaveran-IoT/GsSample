using Infrastructure.Interfaces;

namespace Infrastructure.UnitOfWork;

/// <summary>
/// پیاده‌سازی IUnitOfWorkManager که از ITransactionContext استفاده می‌کند.
/// این کلاس یک wrapper ساده برای ITransactionContext است.
/// </summary>
public sealed class UnitOfWorkManager : IUnitOfWorkManager
{
    private readonly ITransactionContext _transactionContext;

    public UnitOfWorkManager(ITransactionContext transactionContext)
    {
        _transactionContext = transactionContext ?? throw new ArgumentNullException(nameof(transactionContext));
    }

    public async Task<IUnitOfWork> CreateNew(CancellationToken cancellationToken)
    {
        // ✅ استفاده از ConfigureAwait(true) برای حفظ async context
        // این مهم است چون AsyncLocal به async context وابسته است
        var transactionScope = await _transactionContext.BeginTransactionAsync(cancellationToken).ConfigureAwait(true);
        return new UnitOfWork(transactionScope);
    }
}

