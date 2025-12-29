using Infrastructure;
using Infrastructure.Interfaces;
using MediatR;

namespace Application.Handlers;

/// <summary>
/// مثال کامل: استفاده از چند Repository در یک Transaction
/// 
/// این Handler نشان می‌دهد که چگونه:
/// 1. Transaction در Handler شروع می‌شود
/// 2. چند Repository از همان Connection استفاده می‌کنند
/// 3. Read operations داخل Transaction به Write DB می‌روند (برای consistency)
/// 4. Commit/Rollback در Handler انجام می‌شود
/// 5. Connection و Transaction به درستی dispose می‌شوند
/// </summary>
public class MultiRepositoryTransactionExample
{
    private readonly IPersonRepository _personRepository;
    private readonly ITransactionContext _transactionContext;

    public MultiRepositoryTransactionExample(
        IPersonRepository personRepository,
        ITransactionContext transactionContext)
    {
        _personRepository = personRepository;
        _transactionContext = transactionContext;
    }

    /// <summary>
    /// مثال: عملیات پیچیده با چند Repository در یک Transaction
    /// 
    /// سناریو: ایجاد Person جدید و به‌روزرسانی Person موجود در یک Transaction
    /// </summary>
    public async Task<Result> ComplexOperationExample(
        Domain.Models.Person newPerson,
        int existingPersonId,
        Domain.Models.Person updatedPerson,
        CancellationToken cancellationToken)
    {
        // ✅ 1. شروع Transaction در Handler (Application Layer)
        // نکته: Transaction در اینجا شروع می‌شود و Connection ایجاد می‌شود
        await using var transaction = await _transactionContext.BeginTransactionAsync(cancellationToken);

        try
        {
            // ✅ 2. Repository 1: Read Operation (داخل Transaction)
            // نکته: این Read به Write DB می‌رود (نه Read Replica) برای consistency
            var existingPerson = await _personRepository.GetById(existingPersonId, cancellationToken);
            // ✅ Connection: Write DB (از TransactionScope - Connection مشترک)

            // ✅ 3. Repository 2: Write Operation (داخل Transaction)
            // نکته: از همان Connection استفاده می‌کند
            await _personRepository.CreatePerson(newPerson, cancellationToken);
            // ✅ Connection: همان Write DB (Connection مشترک)

            // ✅ 4. Repository 3: Write Operation (داخل Transaction)
            // نکته: از همان Connection استفاده می‌کند
            await _personRepository.UpdatePerson(existingPersonId, updatedPerson, cancellationToken);
            // ✅ Connection: همان Write DB (Connection مشترک)

            // ✅ 5. Repository 4: Read Operation (داخل Transaction)
            // نکته: این Read هم به Write DB می‌رود برای consistency
            var allPersons = await _personRepository.GetAll(cancellationToken);
            // ✅ Connection: همان Write DB (Connection مشترک)

            // ✅ 6. Commit در Handler (Application Layer)
            // نکته: همه عملیات در یک Transaction commit می‌شوند
            await transaction.CommitAsync(cancellationToken);
            // ✅ Connection: هنوز زنده است (برای commit)

            return Result.Success();
        }
        catch (Exception ex)
        {
            // ✅ 7. Rollback در Handler (Application Layer)
            // نکته: اگر Exception رخ دهد، همه عملیات rollback می‌شوند
            await transaction.RollbackAsync(cancellationToken);
            // ✅ Connection: هنوز زنده است (برای rollback)

            return Result.Failure(ex.Message);
        }
        // ✅ 8. DisposeAsync به صورت خودکار صدا زده می‌شود (با await using)
        // نکته: در اینجا Connection و Transaction dispose می‌شوند
        // ✅ اگر commit یا rollback نشده باشد، به صورت خودکار rollback می‌شود
    }

    /// <summary>
    /// مثال: سناریوی واقعی - ایجاد Order با چند عملیات
    /// 
    /// این مثال نشان می‌دهد که چگونه در یک سناریوی واقعی از Transaction استفاده می‌شود
    /// </summary>
    public async Task<Result<int>> CreateOrderExample(
        Domain.Models.Person customer,
        List<Domain.Models.Person> items,
        CancellationToken cancellationToken)
    {
        // ✅ شروع Transaction
        await using var transaction = await _transactionContext.BeginTransactionAsync(cancellationToken);

        try
        {
            // ✅ Step 1: ایجاد Customer (اگر وجود نداشته باشد)
            // Read: بررسی وجود Customer
            Domain.Models.Person? existingCustomer = null;
            try
            {
                existingCustomer = await _personRepository.GetById(customer.Id, cancellationToken);
                // ✅ Connection: Write DB (برای consistency - می‌تواند Customer تازه ایجاد شده را ببیند)
            }
            catch (KeyNotFoundException)
            {
                // Customer وجود ندارد، ایجاد می‌کنیم
                await _personRepository.CreatePerson(customer, cancellationToken);
                // ✅ Connection: همان Write DB (Connection مشترک)
            }

            // ✅ Step 2: ایجاد Items
            foreach (var item in items)
            {
                await _personRepository.CreatePerson(item, cancellationToken);
                // ✅ Connection: همان Write DB (Connection مشترک)
            }

            // ✅ Step 3: Validation - Read برای بررسی
            var allItems = await _personRepository.GetAll(cancellationToken);
            // ✅ Connection: Write DB (برای consistency - می‌تواند Items تازه ایجاد شده را ببیند)

            if (allItems.Count() != items.Count + 1) // +1 for customer
            {
                throw new InvalidOperationException("Validation failed: Item count mismatch");
            }

            // ✅ Commit
            await transaction.CommitAsync(cancellationToken);
            return Result<int>.Success(customer.Id);
        }
        catch (Exception ex)
        {
            // ✅ Rollback
            await transaction.RollbackAsync(cancellationToken);
            return Result<int>.Failure(ex.Message);
        }
        // ✅ DisposeAsync خودکار
    }
}

/// <summary>
/// کلاس کمکی Result برای مثال
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }

    private Result(bool isSuccess, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static Result Success() => new Result(true);
    public static Result Failure(string errorMessage) => new Result(false, errorMessage);
}

/// <summary>
/// کلاس کمکی Result<T> برای مثال
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }

    private Result(bool isSuccess, T? value = default, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
    }

    public static Result<T> Success(T value) => new Result<T>(true, value);
    public static Result<T> Failure(string errorMessage) => new Result<T>(false, default, errorMessage);
}

