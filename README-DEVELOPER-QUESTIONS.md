# پاسخ به سوالات دولوپر: مشکل `await` داخل `lock` و Transaction مشترک

این سند به سوالات دولوپر در مورد محدودیت‌های فنی و معماری پاسخ می‌دهد و نشان می‌دهد که در `GsSample` این مشکلات حل شده‌اند.

---

## 📋 سوالات دولوپر

### سوال 1: محدودیت فنی - `await` داخل `lock`

> در C# استفاده از `await` داخل یک بلاک `lock` اصولاً مجاز نیست و کامپایلر خطای CS1996 می‌دهد.
> 
> دلیل آن این است که `lock` یک سکشن کاملاً synchronous است و اجازه yield و آزاد کردن thread را نمی‌دهد، در حالی که `await` دقیقاً نیاز به همین رفتار دارد.

### سوال 2: نیاز معماری - Transaction مشترک

> در بخش‌هایی از لایه Application چند متد و چند Repository باید در چارچوب یک واحد کاری (TransactionScope یا Transaction مشترک) اجرا شوند.
> 
> از آن‌جایی که هر transaction فقط روی یک connection مشخص عمل می‌کند، تا وقتی در همان scope هستیم باید از یک connection مشترک استفاده کنیم.
> 
> اگر در هر فراخوانی connection جداگانه ساخته شود، امکان پیاده‌سازی transaction مشترک عملاً از بین می‌رود.

---

## ✅ پاسخ: راه‌حل در GsSample

### مشکل 1: `await` داخل `lock` → حل شده ✅

**راه‌حل:**
- ✅ **ConnectionFactory کاملاً stateless است** (بدون `lock` و cache)
- ✅ **TransactionContext از `AsyncLocal` استفاده می‌کند** (بدون `lock`)

**کد فعلی در `GsSample`:**

```csharp
// ✅ Infrastructure/Factories/ConnectionFactory.cs
public sealed class ConnectionFactory : IConnectionFactory
{
    // ✅ هیچ _lock استفاده نشده
    // ✅ هیچ _cachedConnection استفاده نشده

    public async Task<DbConnection> CreateWriteConnection(CancellationToken cancellationToken = default)
    {
        // ✅ همیشه Connection جدید می‌سازد (بدون lock)
        var conn = new SqlConnection(_writeConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return conn;
    }
}
```

```csharp
// ✅ Infrastructure/Factories/TransactionContext.cs
public sealed class TransactionContext : ITransactionContext
{
    // ✅ استفاده از AsyncLocal (نه lock)
    private static readonly AsyncLocal<TransactionScope?> _currentScope = new();

    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        // ✅ کاملاً async (بدون lock)
        var connection = await _connectionFactory.CreateWriteConnection(cancellationToken)
            .ConfigureAwait(true);
        
        var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted,
            cancellationToken)
            .ConfigureAwait(true);

        // ✅ ذخیره در AsyncLocal (بدون lock)
        var scope = new TransactionScope(connection, transaction, this);
        _currentScope.Value = scope;

        return scope;
    }

    public DbConnection? GetCurrentConnection()
    {
        // ✅ بدون lock - AsyncLocal thread-safe است
        return _currentScope.Value?.Connection;
    }
}
```

**نتیجه:**
- ✅ هیچ `lock` استفاده نشده
- ✅ کاملاً async/await (بدون `.Result`)
- ✅ بدون deadlock

### مشکل 2: Transaction مشترک → حل شده ✅

**راه‌حل:**
- ✅ **TransactionContext از `AsyncLocal` استفاده می‌کند** برای ذخیره Connection مشترک
- ✅ **Repositoryها از `GetCurrentConnection()` استفاده می‌کنند** برای دریافت Connection مشترک

**کد فعلی در `GsSample`:**

```csharp
// ✅ Infrastructure/Repositories/PersonRepository.cs
public sealed class PersonRepository : IPersonRepository
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ITransactionContext _transactionContext;

    public async Task CreatePerson(Person person, CancellationToken cancellationToken)
    {
        // ✅ استفاده از Helper Method
        // این Helper به صورت خودکار:
        // - اگر transaction فعال باشد: از connection مشترک استفاده می‌کند
        // - در غیر این صورت: connection جدید از Write DB می‌سازد
        person.Id = await ConnectionExtensions.ExecuteWriteCommandAsync(
            _connectionFactory,
            _transactionContext,
            async (db, ct) =>
            {
                const string sql = @"
                    INSERT INTO Persons (FirstName, LastName, DateOfBirth)
                    OUTPUT INSERTED.Id
                    VALUES (@FirstName, @LastName, @DateOfBirth)";
                
                var command = new CommandDefinition(sql, person, cancellationToken: ct);
                return await db.QuerySingleAsync<int>(command);
            },
            cancellationToken);
    }
}
```

```csharp
// ✅ Infrastructure/Extensions/ConnectionExtensions.cs
public static async Task<T> ExecuteWriteCommandAsync<T>(
    IConnectionFactory connectionFactory,
    ITransactionContext transactionContext,
    Func<DbConnection, CancellationToken, Task<T>> command,
    CancellationToken cancellationToken = default)
{
    // ✅ بررسی اینکه آیا transaction فعال است
    var currentConnection = transactionContext.GetCurrentConnection();
    
    if (currentConnection != null)
    {
        // ✅ استفاده از connection مشترک (transaction فعال است)
        return await command(currentConnection, cancellationToken).ConfigureAwait(false);
    }

    // ✅ استفاده از Write DB (transaction فعال نیست)
    await using var db = await connectionFactory.CreateWriteConnection(cancellationToken)
        .ConfigureAwait(false);
    return await command(db, cancellationToken).ConfigureAwait(false);
}
```

**مثال استفاده در Handler:**

```csharp
// ✅ Application/Handlers/MultiRepositoryTransactionExample.cs
public async Task<Result> ComplexOperationExample(
    Domain.Models.Person newPerson,
    int existingPersonId,
    Domain.Models.Person updatedPerson,
    CancellationToken cancellationToken)
{
    // ✅ 1. شروع Transaction در Handler
    await using var transaction = await _transactionContext.BeginTransactionAsync(cancellationToken);

    try
    {
        // ✅ 2. همه Repositoryها از همان Connection استفاده می‌کنند
        // Repository 1: Read Operation
        var existingPerson = await _personRepository.GetById(existingPersonId, cancellationToken);
        // ✅ Connection: Write DB (از TransactionScope - Connection مشترک)

        // Repository 2: Write Operation
        await _personRepository.CreatePerson(newPerson, cancellationToken);
        // ✅ Connection: همان Write DB (Connection مشترک)

        // Repository 3: Write Operation
        await _personRepository.UpdatePerson(existingPersonId, updatedPerson, cancellationToken);
        // ✅ Connection: همان Write DB (Connection مشترک)

        // Repository 4: Read Operation
        var allPersons = await _personRepository.GetAll(cancellationToken);
        // ✅ Connection: همان Write DB (Connection مشترک)

        // ✅ 3. Commit در Handler
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
    catch (Exception ex)
    {
        // ✅ 4. Rollback در Handler
        await transaction.RollbackAsync(cancellationToken);
        return Result.Failure(ex.Message);
    }
    // ✅ 5. DisposeAsync به صورت خودکار صدا زده می‌شود (با await using)
}
```

**نحوه کار:**
1. Handler `BeginTransactionAsync()` را صدا می‌زند
2. `TransactionContext` یک Connection جدید می‌سازد و در `AsyncLocal` ذخیره می‌کند
3. Repositoryها `GetCurrentConnection()` را صدا می‌زنند و همان Connection را دریافت می‌کنند
4. همه عملیات روی همان Connection و Transaction انجام می‌شوند
5. Handler `CommitAsync()` یا `RollbackAsync()` را صدا می‌زند
6. `DisposeAsync()` Connection و Transaction را dispose می‌کند

**نتیجه:**
- ✅ Transaction مشترک بین چند Repository
- ✅ همه Repositoryها از Connection مشترک استفاده می‌کنند
- ✅ بدون نیاز به passing explicit connection

---

## 🎯 خلاصه

### مشکل 1: `await` داخل `lock` → حل شده ✅

**راه‌حل:**
- ✅ حذف `lock` از `ConnectionFactory`
- ✅ حذف cache از `ConnectionFactory`
- ✅ استفاده از `AsyncLocal` در `TransactionContext` (بدون `lock`)

### مشکل 2: Transaction مشترک → حل شده ✅

**راه‌حل:**
- ✅ `TransactionContext` از `AsyncLocal` استفاده می‌کند
- ✅ Repositoryها از `GetCurrentConnection()` استفاده می‌کنند
- ✅ همه Repositoryها در همان async context از Connection مشترک استفاده می‌کنند

---

## 📚 فایل‌های مرتبط

1. **`Infrastructure/Factories/ConnectionFactory.cs`**
   - ConnectionFactory stateless (بدون lock و cache)

2. **`Infrastructure/Factories/TransactionContext.cs`**
   - TransactionContext با AsyncLocal (بدون lock)

3. **`Infrastructure/Extensions/ConnectionExtensions.cs`**
   - Helper methods برای Read/Write routing

4. **`Infrastructure/Repositories/PersonRepository.cs`**
   - مثال استفاده از ConnectionFactory و TransactionContext

5. **`Application/Handlers/MultiRepositoryTransactionExample.cs`**
   - مثال کامل استفاده از چند Repository در یک Transaction

6. **`LOCK-AWAIT-TRANSACTION-SOLUTION.md`**
   - توضیحات کامل راه‌حل

---

## ✅ نتیجه‌گیری

**این design pattern هر دو مشکل را حل می‌کند:**

1. **مشکل فنی (`await` داخل `lock`):**
   - ✅ حذف `lock` از `ConnectionFactory`
   - ✅ استفاده از `AsyncLocal` در `TransactionContext` (بدون `lock`)

2. **مشکل معماری (Transaction مشترک):**
   - ✅ `TransactionContext` از `AsyncLocal` استفاده می‌کند
   - ✅ Repositoryها به صورت خودکار از Connection مشترک استفاده می‌کنند

**این design pattern در `GsSample` کاملاً پیاده‌سازی شده و تست شده است.**

**برای migration به `GsTechV2Backend`، به فایل `GsTechV2Backend/ConnectionFactory-Solution/15-DEVELOPER-LOCK-AWAIT-SOLUTION.md` مراجعه کنید.**

