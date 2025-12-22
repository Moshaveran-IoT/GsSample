# 📚 توضیح کامل Transaction Context و مدیریت Transaction

این مستند به تمام سوالات شما درباره Transaction Context، Scope، Lifetime و نحوه مدیریت Transaction پاسخ می‌دهد.

---

## 🎯 سوالات اصلی

### 1️⃣ تراکنش کجا نگهداری می‌شود؟

**پاسخ:** تراکنش در **`AsyncLocal<TransactionScope?>`** نگهداری می‌شود.

```21:21:GsSample/Infrastructure/Factories/TransactionContext.cs
    private static readonly AsyncLocal<TransactionScope?> _currentScope = new();
```

**چرا AsyncLocal؟**
- هر **async context** (thread) یک transaction مستقل دارد
- Thread-safe است بدون نیاز به lock
- با async/await کاملاً سازگار است
- هر scope یک transaction دارد

---

### 2️⃣ Transaction Context کجا نگهداری می‌شود؟

**پاسخ:** Transaction Context در **DI Container** به صورت **Scoped** ثبت می‌شود.

```35:40:GsSample/Infrastructure/Extensions/ServiceCollectionExtensions.cs
        // ثبت TransactionContext به صورت Scoped
        services.AddScoped<ITransactionContext>(sp =>
        {
            var connectionFactory = sp.GetRequiredService<IConnectionFactory>();
            return new TransactionContext(connectionFactory);
        });
```

**مهم:** 
- به ازای هر **HTTP Request** یا **Scope** یک instance از `TransactionContext` ایجاد می‌شود
- این instance در کل scope زنده است
- اما **Transaction خودش** در `AsyncLocal` نگهداری می‌شود

---

### 3️⃣ Transaction Context چیست و چه کاری انجام می‌دهد؟

**Transaction Context** یک کلاس است که:
1. **Connection** و **Transaction** را مدیریت می‌کند
2. آنها را در `AsyncLocal` ذخیره می‌کند
3. Repositoryها می‌توانند از همان connection استفاده کنند

```19:57:GsSample/Infrastructure/Factories/TransactionContext.cs
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
```

---

### 4️⃣ Transaction Scope چیست؟

**Transaction Scope** یک کلاس داخلی است که:
- **Connection** را نگه می‌دارد
- **Transaction** را نگه می‌دارد
- **Commit** و **Rollback** را مدیریت می‌کند
- **Dispose** را مدیریت می‌کند (اگر commit نشده باشد، rollback می‌کند)

```86:101:GsSample/Infrastructure/Factories/TransactionContext.cs
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
```

---

### 5️⃣ به ازای هر Scope چند Transaction ایجاد می‌شود؟

**پاسخ:** به ازای هر **BeginTransactionAsync** یک Transaction ایجاد می‌شود.

**مهم:** 
- در یک async context فقط **یک Transaction** می‌تواند فعال باشد
- اگر بخواهید Transaction جدید شروع کنید، باید اول Transaction قبلی را commit یا rollback کنید

```35:42:GsSample/Infrastructure/Factories/TransactionContext.cs
        // اگر قبلاً یک transaction در این context شروع شده باشد، خطا می‌دهیم
        if (_currentScope.Value != null)
        {
            throw new InvalidOperationException(
                "A transaction is already active in this async context. " +
                "Nested transactions are not supported. " +
                "Please commit or rollback the current transaction first.");
        }
```

---

### 6️⃣ Transaction کجا ایجاد می‌شود؟

**پاسخ:** Transaction در **Handler** ایجاد می‌شود.

```22:23:GsSample/Application/Handlers/CreatePersonCommandHandler.cs
        // ✅ استفاده از Transaction برای اطمینان از atomicity
        await using var transaction = await transactionContext.BeginTransactionAsync(cancellationToken);
```

**جریان کار:**
1. Handler از DI Container `ITransactionContext` را دریافت می‌کند
2. Handler `BeginTransactionAsync()` را صدا می‌زند
3. TransactionContext یک Connection می‌سازد
4. TransactionContext یک Transaction شروع می‌کند
5. TransactionScope در AsyncLocal ذخیره می‌شود

---

### 7️⃣ کی Commit می‌کنیم؟ کی Rollback می‌کنیم؟

**Commit:** وقتی همه عملیات موفقیت‌آمیز باشند

```25:30:GsSample/Application/Handlers/CreatePersonCommandHandler.cs
        try
        {
            await personRepository.CreatePerson(request.Person, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            return new CreatePersonCommandResponse(request.Person.Id);
        }
```

**Rollback:** وقتی Exception رخ دهد

```32:36:GsSample/Application/Handlers/CreatePersonCommandHandler.cs
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
```

**Auto Rollback:** اگر Transaction commit نشود و dispose شود، به صورت خودکار rollback می‌شود

```133:149:GsSample/Infrastructure/Factories/TransactionContext.cs
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
```

---

### 8️⃣ Repository چگونه از Transaction استفاده می‌کند؟

**Repository** از `TransactionContext.GetCurrentConnection()` استفاده می‌کند:

```24:38:GsSample/Infrastructure/Extensions/ConnectionExtensions.cs
    public static async Task<DbConnection> GetReadConnectionAsync(
        IConnectionFactory connectionFactory,
        ITransactionContext transactionContext,
        CancellationToken cancellationToken = default)
    {
        // ✅ اگر transaction فعال باشد، از Write DB استفاده می‌کنیم (برای consistency)
        var currentConnection = transactionContext.GetCurrentConnection();
        if (currentConnection != null)
        {
            return currentConnection;
        }

        // ✅ در غیر این صورت، از Read Replica استفاده می‌کنیم
        return await connectionFactory.CreateReadConnection(cancellationToken).ConfigureAwait(false);
    }
```

**مثال استفاده در Repository:**

```84:100:GsSample/Infrastructure/Repositories/PersonRepository.cs
    public async Task CreatePerson(Person person, CancellationToken cancellationToken)
    {
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
```

---

### 9️⃣ Lifetime Transaction چیست؟

**Lifetime Transaction** یعنی:
- **شروع:** وقتی `BeginTransactionAsync()` صدا زده می‌شود
- **زندگی:** تا وقتی که `CommitAsync()` یا `RollbackAsync()` صدا زده شود
- **پایان:** وقتی `DisposeAsync()` صدا زده می‌شود (که به صورت خودکار در `await using` اتفاق می‌افتد)

**مثال:**

```22:36:GsSample/Application/Handlers/CreatePersonCommandHandler.cs
        // ✅ استفاده از Transaction برای اطمینان از atomicity
        await using var transaction = await transactionContext.BeginTransactionAsync(cancellationToken);
        
        try
        {
            await personRepository.CreatePerson(request.Person, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            return new CreatePersonCommandResponse(request.Person.Id);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
```

**زمان‌بندی:**
1. `BeginTransactionAsync()` → Transaction شروع می‌شود
2. عملیات Repository → در همان Transaction انجام می‌شود
3. `CommitAsync()` → Transaction commit می‌شود
4. `DisposeAsync()` → Connection و Transaction dispose می‌شوند (به صورت خودکار)

---

### 🔟 جایگاه Context در معماری

**سه لایه:**

1. **Handler Layer (Application Layer)**
   - Transaction را شروع می‌کند (`BeginTransactionAsync`)
   - عملیات را انجام می‌دهد
   - Commit یا Rollback می‌کند

2. **Repository Layer (Data Access Layer)**
   - از `TransactionContext.GetCurrentConnection()` استفاده می‌کند
   - اگر Transaction فعال باشد، از همان Connection استفاده می‌کند
   - اگر Transaction فعال نباشد، Connection جدید می‌سازد

3. **Infrastructure Layer**
   - `TransactionContext` را پیاده‌سازی می‌کند
   - `AsyncLocal` را مدیریت می‌کند
   - Connection و Transaction را نگهداری می‌کند

---

## 📊 نمودار جریان کار

```
┌─────────────────────────────────────────────────────────────┐
│                    HTTP Request / Scope                      │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Handler (Application Layer)                         │  │
│  │                                                       │  │
│  │  1. await using var transaction =                    │  │
│  │     await _transactionContext.BeginTransactionAsync() │  │
│  │                                                       │  │
│  │     ↓                                                 │  │
│  │  TransactionContext.BeginTransactionAsync()           │  │
│  │    - Create Connection                                │  │
│  │    - Begin Transaction                                │  │
│  │    - Store in AsyncLocal                              │  │
│  │                                                       │  │
│  │  2. await _personRepository.CreatePerson(...)        │  │
│  │                                                       │  │
│  │     ↓                                                 │  │
│  │  Repository.GetWriteConnectionAsync()                │  │
│  │    - Check TransactionContext.GetCurrentConnection() │  │
│  │    - Return same Connection (if transaction active)  │  │
│  │                                                       │  │
│  │  3. await transaction.CommitAsync()                  │  │
│  │                                                       │  │
│  │  4. DisposeAsync() (automatic)                       │  │
│  │    - Rollback if not committed                       │  │
│  │    - Close Connection                                │  │
│  │    - Clear AsyncLocal                                │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  TransactionContext (Infrastructure Layer)            │  │
│  │                                                       │  │
│  │  AsyncLocal<TransactionScope?> _currentScope         │  │
│  │    └─> TransactionScope                              │  │
│  │         ├─> Connection                               │  │
│  │         └─> Transaction                              │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## ✅ خلاصه پاسخ‌ها

| سوال | پاسخ |
|------|------|
| **تراکنش کجا نگهداری می‌شود؟** | در `AsyncLocal<TransactionScope?>` |
| **Transaction Context کجا نگهداری می‌شود؟** | در DI Container به صورت Scoped |
| **Transaction Context چیست؟** | کلاسی که Connection و Transaction را مدیریت می‌کند |
| **Transaction Scope چیست؟** | کلاسی که Connection و Transaction را نگه می‌دارد و Commit/Rollback را مدیریت می‌کند |
| **به ازای هر Scope چند Transaction؟** | به ازای هر `BeginTransactionAsync` یک Transaction |
| **Transaction کجا ایجاد می‌شود؟** | در Handler با صدا زدن `BeginTransactionAsync()` |
| **کی Commit می‌کنیم؟** | وقتی همه عملیات موفقیت‌آمیز باشند |
| **کی Rollback می‌کنیم؟** | وقتی Exception رخ دهد یا به صورت خودکار در Dispose |
| **Repository چگونه استفاده می‌کند؟** | از `GetCurrentConnection()` برای دریافت Connection مشترک |
| **Lifetime Transaction چیست؟** | از `BeginTransactionAsync()` تا `DisposeAsync()` |
| **جایگاه Context؟** | در Infrastructure Layer، استفاده در Handler Layer |

---

## ⚠️ نکته مهم: Dispose و بستن Connection

**سوال:** آیا `ClearScope()` فقط متغیرها را null می‌کند یا Connection را هم می‌بندد؟

**پاسخ:** `ClearScope()` فقط AsyncLocal را null می‌کند، اما **`DisposeAsync()`** خودش Connection و Transaction را به درستی dispose می‌کند:

```133:159:GsSample/Infrastructure/Factories/TransactionContext.cs
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
```

**ترتیب Dispose:**
1. ✅ Rollback (اگر commit نشده باشد)
2. ✅ ClearScope (null کردن AsyncLocal - فقط برای پاک کردن reference)
3. ✅ Dispose Transaction (بستن Transaction)
4. ✅ Dispose Connection (بستن Connection به صورت کامل)

**مهم:** `DisposeAsync()` Connection را **کاملاً می‌بندد**، نه فقط null می‌کند. `ClearScope()` فقط reference را از AsyncLocal پاک می‌کند.

---

## 🎯 نکات مهم

1. **AsyncLocal** برای هر async context یک Transaction مستقل نگه می‌دارد
2. **TransactionContext** در DI به صورت Scoped ثبت می‌شود (یک instance per request)
3. **Transaction** در Handler شروع می‌شود و در همان Handler پایان می‌یابد
4. **Repository** به صورت خودکار از Transaction استفاده می‌کند (اگر فعال باشد)
5. **Auto Rollback** در Dispose اگر Transaction commit نشده باشد
6. **Connection Sharing** بین Repositoryها در یک Transaction
7. **DisposeAsync** Connection و Transaction را به درستی می‌بندد (نه فقط null می‌کند)

---

## 📝 مثال کامل

```csharp
// Handler
public async Task<Result> Handle(Command cmd, CancellationToken ct)
{
    // 1. شروع Transaction
    await using var transaction = await _transactionContext.BeginTransactionAsync(ct);
    
    try
    {
        // 2. عملیات Repository (از همان Connection استفاده می‌کنند)
        await _repo1.SaveAsync(data1, ct);
        await _repo2.SaveAsync(data2, ct);
        await _repo3.UpdateAsync(data3, ct);
        
        // 3. Commit
        await transaction.CommitAsync(ct);
        return Result.Success();
    }
    catch
    {
        // 4. Rollback در صورت خطا
        await transaction.RollbackAsync(ct);
        throw;
    }
    // 5. DisposeAsync به صورت خودکار صدا زده می‌شود
}
```

---

**پایان مستند**

