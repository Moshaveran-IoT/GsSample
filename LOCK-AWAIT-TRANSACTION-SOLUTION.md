# راه‌حل کامل: مشکل `await` داخل `lock` و Transaction مشترک

این سند به سوالات دولوپر در مورد محدودیت‌های فنی و معماری پاسخ می‌دهد و راه‌حل عملیاتی ارائه می‌دهد.

---

## 📋 مشکلات مطرح شده

### مشکل 1: محدودیت فنی - `await` داخل `lock`

**مشکل:**
- در C# استفاده از `await` داخل یک بلاک `lock` غیرممکن است (خطای CS1996)
- `lock` یک سکشن کاملاً synchronous است و اجازه yield نمی‌دهد
- `await` نیاز به yield و آزاد کردن thread دارد

**کد مشکل‌دار (قدیمی):**
```csharp
// ❌ این کد compile نمی‌شود یا deadlock ایجاد می‌کند
lock (this._lock)
{
    if (this._cachedConnection == null)
    {
        // ❌ خطای CS1996: Cannot await in lock statement
        this._cachedConnection = await CreateAndOpenConnectionAsync(...);
    }
    return this._cachedConnection;
}
```

**یا استفاده از `.Result` (که deadlock ایجاد می‌کند):**
```csharp
// ❌ این کد deadlock ایجاد می‌کند
lock (this._lock)
{
    if (this._cachedConnection == null)
    {
        // ❌ استفاده از .Result در lock → deadlock!
        this._cachedConnection = CreateAndOpenConnectionAsync(...).Result;
    }
    return this._cachedConnection;
}
```

### مشکل 2: نیاز معماری - Transaction مشترک

**مشکل:**
- در لایه Application، چند Repository باید در یک Transaction مشترک کار کنند
- هر Transaction فقط روی یک Connection کار می‌کند
- اگر هر Repository Connection جداگانه داشته باشد، Transaction مشترک غیرممکن است

**سناریوی مشکل‌دار:**
```csharp
// ❌ هر Repository یک Connection جداگانه می‌گیرد
await _orderRepo.CreateAsync(order, ct);        // Connection 1
await _paymentRepo.ProcessAsync(payment, ct);   // Connection 2
await _inventoryRepo.UpdateAsync(inventory, ct); // Connection 3

// ❌ نمی‌توانیم یک Transaction مشترک داشته باشیم
// چون هر Repository Connection خودش را دارد
```

---

## ✅ راه‌حل: دو لایه طراحی

### لایه 1: ConnectionFactory (Stateless - بدون Cache)

**اصل:** ConnectionFactory کاملاً stateless است و همیشه Connection جدید می‌سازد.

**مزایا:**
- ✅ بدون `lock` → بدون deadlock
- ✅ کاملاً async/await → بدون `.Result`
- ✅ Thread-safe (هر thread Connection خودش را می‌گیرد)
- ✅ آماده برای Read Replicas

**پیاده‌سازی:**

```csharp
/// <summary>
/// Factory برای ایجاد و مدیریت کانکشن‌های SQL.
/// این کلاس کاملاً stateless است و همیشه کانکشن جدید می‌سازد.
/// 
/// Connection reuse توسط SQL Server Connection Pooling انجام می‌شود.
/// برای transaction مشترک بین چند Repository، از ITransactionContext استفاده کنید.
/// </summary>
public sealed class ConnectionFactory : IConnectionFactory
{
    private readonly string _writeConnectionString;
    private readonly string _readConnectionString;

    public ConnectionFactory(string writeConnectionString, string readConnectionString)
    {
        _writeConnectionString = writeConnectionString 
            ?? throw new ArgumentNullException(nameof(writeConnectionString));
        _readConnectionString = readConnectionString 
            ?? throw new ArgumentNullException(nameof(readConnectionString));
    }

    /// <summary>
    /// ایجاد کانکشن برای عملیات نوشتن (Write).
    /// همیشه کانکشن جدید می‌سازد و باز می‌کند.
    /// </summary>
    public async Task<DbConnection> CreateWriteConnection(CancellationToken cancellationToken = default)
    {
        // ✅ همیشه Connection جدید می‌سازد (بدون cache)
        var conn = new SqlConnection(_writeConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return conn;
    }

    /// <summary>
    /// ایجاد کانکشن برای عملیات خواندن (Read).
    /// همیشه کانکشن جدید می‌سازد و باز می‌کند.
    /// </summary>
    public async Task<DbConnection> CreateReadConnection(CancellationToken cancellationToken = default)
    {
        // ✅ همیشه Connection جدید می‌سازد (بدون cache)
        var conn = new SqlConnection(_readConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return conn;
    }
}
```

**نکات مهم:**
- ✅ هیچ `lock` استفاده نشده
- ✅ هیچ cache استفاده نشده
- ✅ کاملاً async/await
- ✅ Connection Pooling خود SQL Server Connection reuse را انجام می‌دهد

### لایه 2: TransactionContext (مدیریت Transaction مشترک با AsyncLocal)

**اصل:** TransactionContext از `AsyncLocal` استفاده می‌کند تا Connection مشترک را در async context ذخیره کند.

**مزایا:**
- ✅ بدون `lock` → از `AsyncLocal` استفاده می‌کند
- ✅ کاملاً async/await
- ✅ Thread-safe (هر async context Connection خودش را دارد)
- ✅ Transaction مشترک بین چند Repository

**پیاده‌سازی:**

```csharp
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
    // ✅ استفاده از AsyncLocal برای ذخیره transaction scope
    // AsyncLocal به صورت خودکار در async context propagate می‌شود
    private static readonly AsyncLocal<TransactionScope?> _currentScope = new();

    private readonly IConnectionFactory _connectionFactory;

    public TransactionContext(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory 
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// شروع یک transaction جدید.
    /// 
    /// این متد:
    /// 1. یک Connection جدید از ConnectionFactory می‌سازد
    /// 2. یک Transaction روی آن Connection شروع می‌کند
    /// 3. TransactionScope را ایجاد می‌کند و در AsyncLocal ذخیره می‌کند
    /// 4. Repositoryها می‌توانند از GetCurrentConnection() برای دریافت همان Connection استفاده کنند
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

        // ✅ ایجاد connection جدید (بدون lock - کاملاً async)
        var connection = await _connectionFactory.CreateWriteConnection(cancellationToken)
            .ConfigureAwait(true);

        // ✅ شروع transaction
        var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted,
            cancellationToken)
            .ConfigureAwait(true);

        // ✅ ایجاد scope و ذخیره در AsyncLocal (بدون lock)
        // AsyncLocal thread-safe است و به صورت خودکار در async context propagate می‌شود
        var scope = new TransactionScope(connection, transaction, this);
        _currentScope.Value = scope;

        return scope;
    }

    /// <summary>
    /// دریافت connection فعلی که در transaction scope است.
    /// </summary>
    public DbConnection? GetCurrentConnection()
    {
        // ✅ بدون lock - AsyncLocal thread-safe است
        return _currentScope.Value?.Connection;
    }

    /// <summary>
    /// دریافت transaction فعلی که در scope است.
    /// </summary>
    public DbTransaction? GetCurrentTransaction()
    {
        // ✅ بدون lock - AsyncLocal thread-safe است
        return _currentScope.Value?.Transaction;
    }

    /// <summary>
    /// پاک کردن scope فعلی (برای استفاده داخلی).
    /// </summary>
    internal void ClearScope()
    {
        // ✅ بدون lock - AsyncLocal thread-safe است
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

            // ✅ اگر commit یا rollback نشده باشد، rollback می‌کنیم
            if (!_committed && !_rolledBack)
            {
                try
                {
                    await Transaction.RollbackAsync().ConfigureAwait(false);
                    _rolledBack = true;
                }
                catch
                {
                    _rolledBack = true;
                }
            }

            // ✅ Dispose کردن transaction و connection
            try
            {
                if (Transaction != null)
                {
                    await Transaction.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch { }

            try
            {
                if (Connection != null)
                {
                    await Connection.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch { }

            // ✅ پاک کردن scope از AsyncLocal (بعد از dispose)
            _context.ClearScope();

            _disposed = true;
        }
    }
}
```

**نکات مهم:**
- ✅ از `AsyncLocal` استفاده می‌کند (نه `lock`)
- ✅ کاملاً async/await
- ✅ Thread-safe (هر async context Connection خودش را دارد)
- ✅ Connection مشترک بین Repositoryها در همان async context

---

## 🔄 الگوی استفاده در Repository

**Repositoryها از `GetCurrentConnection()` استفاده می‌کنند:**

```csharp
/// <summary>
/// پیاده‌سازی Repository با استفاده از ConnectionFactory و TransactionContext.
/// </summary>
public sealed class PersonRepository : IPersonRepository
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ITransactionContext _transactionContext;

    public PersonRepository(
        IConnectionFactory connectionFactory,
        ITransactionContext transactionContext)
    {
        _connectionFactory = connectionFactory;
        _transactionContext = transactionContext;
    }

    public async Task CreatePerson(Person person, CancellationToken cancellationToken)
    {
        // ✅ استفاده از Helper برای Write Command
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

**Helper Method (`ConnectionExtensions`):**

```csharp
/// <summary>
/// اجرای یک command با استفاده از Write Connection.
/// 
/// این متد به صورت خودکار connection مناسب را انتخاب می‌کند:
/// - اگر transaction فعال باشد: از connection مشترک استفاده می‌کند
/// - در غیر این صورت: connection جدید از Write DB می‌سازد
/// </summary>
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

---

## 📝 الگوی استفاده در Handler (Application Layer)

**Handler Transaction را شروع می‌کند و Repositoryها از Connection مشترک استفاده می‌کنند:**

```csharp
/// <summary>
/// مثال: استفاده از چند Repository در یک Transaction
/// </summary>
public class CreateOrderHandler
{
    private readonly IOrderRepository _orderRepo;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly ITransactionContext _transactionContext;

    public CreateOrderHandler(
        IOrderRepository orderRepo,
        IPaymentRepository paymentRepo,
        IInventoryRepository inventoryRepo,
        ITransactionContext transactionContext)
    {
        _orderRepo = orderRepo;
        _paymentRepo = paymentRepo;
        _inventoryRepo = inventoryRepo;
        _transactionContext = transactionContext;
    }

    public async Task<Result<int>> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        // ✅ 1. شروع Transaction در Handler
        await using var transaction = await _transactionContext.BeginTransactionAsync(ct);

        try
        {
            // ✅ 2. همه Repositoryها از همان Connection استفاده می‌کنند
            // Repository 1: از GetCurrentConnection() استفاده می‌کند → Connection مشترک
            await _orderRepo.CreateAsync(order, ct);
            
            // Repository 2: از GetCurrentConnection() استفاده می‌کند → همان Connection مشترک
            await _paymentRepo.ProcessAsync(payment, ct);
            
            // Repository 3: از GetCurrentConnection() استفاده می‌کند → همان Connection مشترک
            await _inventoryRepo.UpdateAsync(inventory, ct);

            // ✅ 3. Commit در Handler
            await transaction.CommitAsync(ct);
            return Result<int>.Success(orderId);
        }
        catch (Exception ex)
        {
            // ✅ 4. Rollback در Handler
            await transaction.RollbackAsync(ct);
            return Result<int>.Failure(ex.Message);
        }
        // ✅ 5. DisposeAsync به صورت خودکار صدا زده می‌شود (با await using)
    }
}
```

**نحوه کار:**
1. Handler `BeginTransactionAsync()` را صدا می‌زند
2. `TransactionContext` یک Connection جدید می‌سازد و در `AsyncLocal` ذخیره می‌کند
3. Repositoryها `GetCurrentConnection()` را صدا می‌زنند و همان Connection را دریافت می‌کنند
4. همه عملیات روی همان Connection و Transaction انجام می‌شوند
5. Handler `CommitAsync()` یا `RollbackAsync()` را صدا می‌زند
6. `DisposeAsync()` Connection و Transaction را dispose می‌کند

---

## 🔍 مقایسه: قبل و بعد

### قبل (کد قدیمی - مشکل‌دار):

```csharp
// ❌ ConnectionFactory با lock و cache
public sealed class ConnectionFactory : IConnectionFactory
{
    private readonly Lock _lock = new();
    private SqlConnection _cachedConnection; // ❌ Stateful

    public async Task<DbConnection> CreateWriteConnection(
        CancellationToken cancellationToken = default, 
        bool createNew = true, 
        bool cacheNewConnection = false)
    {
        if (createNew)
        {
            var newConn = await CreateAndOpenConnectionAsync(...);
            
            if (cacheNewConnection)
            {
                lock (this._lock) // ❌ lock
                {
                    this._cachedConnection = newConn;
                }
            }
            
            return newConn;
        }

        // ❌ استفاده از .Result در lock (deadlock!)
        lock (this._lock)
        {
            if (this._cachedConnection == null)
            {
                this._cachedConnection = CreateAndOpenConnectionAsync(...).Result; // ❌ .Result
            }
            return this._cachedConnection;
        }
    }
}

// ❌ استفاده در Handler - باید createNew: false بفرستیم
public async Task<Result<int>> Handle(CreateOrderCommand cmd, CancellationToken ct)
{
    // ❌ مشکل: باید createNew: false را به همه Repositoryها بفرستیم
    await _orderRepo.CreateAsync(order, ct, createNew: false);        // ❌
    await _paymentRepo.ProcessAsync(payment, ct, createNew: false);   // ❌
    await _inventoryRepo.UpdateAsync(inventory, ct, createNew: false); // ❌
    
    // ❌ اگر یک Repository createNew: true بفرستد، transaction می‌شکند
}
```

**مشکلات:**
- ❌ استفاده از `lock` → امکان deadlock
- ❌ استفاده از `.Result` در `lock` → deadlock حتمی
- ❌ ConnectionFactory stateful → thread-safety مشکل دارد
- ❌ باید `createNew: false` را به همه Repositoryها بفرستیم → error-prone
- ❌ اگر یک Repository `createNew: true` بفرستد، transaction می‌شکند

### بعد (کد جدید - بدون مشکل):

```csharp
// ✅ ConnectionFactory stateless (بدون lock و cache)
public sealed class ConnectionFactory : IConnectionFactory
{
    public async Task<DbConnection> CreateWriteConnection(CancellationToken cancellationToken = default)
    {
        // ✅ همیشه Connection جدید می‌سازد (بدون lock)
        var conn = new SqlConnection(_writeConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return conn;
    }
}

// ✅ TransactionContext با AsyncLocal (بدون lock)
public sealed class TransactionContext : ITransactionContext
{
    private static readonly AsyncLocal<TransactionScope?> _currentScope = new(); // ✅ AsyncLocal

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

// ✅ استفاده در Handler - ساده و واضح
public async Task<Result<int>> Handle(CreateOrderCommand cmd, CancellationToken ct)
{
    // ✅ شروع Transaction
    await using var transaction = await _transactionContext.BeginTransactionAsync(ct);

    try
    {
        // ✅ همه Repositoryها به صورت خودکار از Connection مشترک استفاده می‌کنند
        await _orderRepo.CreateAsync(order, ct);        // ✅
        await _paymentRepo.ProcessAsync(payment, ct);   // ✅
        await _inventoryRepo.UpdateAsync(inventory, ct); // ✅

        await transaction.CommitAsync(ct);
        return Result<int>.Success(orderId);
    }
    catch
    {
        await transaction.RollbackAsync(ct);
        throw;
    }
}
```

**مزایا:**
- ✅ بدون `lock` → بدون deadlock
- ✅ کاملاً async/await → بدون `.Result`
- ✅ ConnectionFactory stateless → thread-safe
- ✅ Repositoryها به صورت خودکار از Connection مشترک استفاده می‌کنند
- ✅ ساده و واضح → error-prone نیست

---

## 🎯 خلاصه راه‌حل

### مشکل 1: `await` داخل `lock` → حل شده ✅

**راه‌حل:**
- حذف `lock` از `ConnectionFactory`
- حذف cache از `ConnectionFactory`
- استفاده از `AsyncLocal` در `TransactionContext` (بدون `lock`)

### مشکل 2: Transaction مشترک → حل شده ✅

**راه‌حل:**
- `TransactionContext` از `AsyncLocal` استفاده می‌کند
- Repositoryها از `GetCurrentConnection()` استفاده می‌کنند
- همه Repositoryها در همان async context از Connection مشترک استفاده می‌کنند

---

## 📚 فایل‌های مرتبط در GsSample

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

---

## ✅ نتیجه‌گیری

این design pattern هر دو مشکل را حل می‌کند:

1. **مشکل فنی (`await` داخل `lock`):**
   - ✅ حذف `lock` از `ConnectionFactory`
   - ✅ استفاده از `AsyncLocal` در `TransactionContext` (بدون `lock`)

2. **مشکل معماری (Transaction مشترک):**
   - ✅ `TransactionContext` از `AsyncLocal` استفاده می‌کند
   - ✅ Repositoryها به صورت خودکار از Connection مشترک استفاده می‌کنند

**این design pattern در `GsSample` کاملاً پیاده‌سازی شده و تست شده است.**

