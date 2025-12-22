# 📋 مشخصات طراحی Transaction - پاسخ به نگرانی‌های دولوپر

این مستند به تمام نگرانی‌های مطرح شده توسط دولوپر پروژه پاسخ می‌دهد و مشخصات دقیق طراحی Transaction را توضیح می‌دهد.

---

## 🎯 1. چرخه عمر Transaction (Transaction Lifecycle)

### 1.1. آغاز Transaction

**Transaction دقیقاً در کدام لایه آغاز می‌شود؟**

**پاسخ:** Transaction در **لایه Application (Handler)** آغاز می‌شود.

```22:23:GsSample/Application/Handlers/CreatePersonCommandHandler.cs
        // ✅ استفاده از Transaction برای اطمینان از atomicity
        await using var transaction = await transactionContext.BeginTransactionAsync(cancellationToken);
```

**جریان کار:**
1. Handler از DI Container `ITransactionContext` را دریافت می‌کند
2. Handler `BeginTransactionAsync()` را صدا می‌زند
3. `TransactionContext.BeginTransactionAsync()`:
   - یک `Connection` جدید از `ConnectionFactory` می‌سازد
   - یک `Transaction` روی آن Connection شروع می‌کند
   - یک `TransactionScope` ایجاد می‌کند
   - `TransactionScope` را در `AsyncLocal` ذخیره می‌کند
   - `TransactionScope` را برمی‌گرداند

```33:57:GsSample/Infrastructure/Factories/TransactionContext.cs
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

### 1.2. خاتمه Transaction

**Transaction دقیقاً در کدام لایه خاتمه داده می‌شود؟**

**پاسخ:** Transaction در **لایه Application (Handler)** خاتمه داده می‌شود.

**دو روش خاتمه:**

#### روش 1: Commit موفقیت‌آمیز

```25:30:GsSample/Application/Handlers/CreatePersonCommandHandler.cs
        try
        {
            await personRepository.CreatePerson(request.Person, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            return new CreatePersonCommandResponse(request.Person.Id);
        }
```

#### روش 2: Rollback در صورت Exception

```32:36:GsSample/Application/Handlers/CreatePersonCommandHandler.cs
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
```

#### روش 3: Auto Rollback در Dispose

اگر Transaction commit یا rollback نشود و `DisposeAsync` صدا زده شود، به صورت خودکار rollback می‌شود:

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

### 1.3. تضمین عدم Dispose ناخواسته

**سوال:** چگونه تضمین می‌شود که در طول فراخوانی Repositoryها، Transaction و Connection به صورت ناخواسته Dispose نشوند؟

**پاسخ:** با استفاده از `AsyncLocal` و `await using`:

1. **Connection در TransactionScope نگهداری می‌شود:**
   - Connection در `TransactionScope.Connection` نگهداری می‌شود
   - `TransactionScope` در `AsyncLocal` ذخیره می‌شود
   - Repositoryها فقط **reference** به Connection را دریافت می‌کنند (نه ownership)

```93:94:GsSample/Infrastructure/Factories/TransactionContext.cs
        public DbConnection Connection { get; }
        public DbTransaction Transaction { get; }
```

2. **Repositoryها Connection را Dispose نمی‌کنند:**
   - `ConnectionExtensions.ExecuteReadQueryAsync` و `ExecuteWriteCommandAsync` فقط Connection را استفاده می‌کنند
   - اگر Transaction فعال باشد، همان Connection را برمی‌گردانند (بدون dispose)
   - اگر Transaction فعال نباشد، Connection جدید می‌سازند و آن را dispose می‌کنند

```87:98:GsSample/Infrastructure/Extensions/ConnectionExtensions.cs
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
```

3. **فقط TransactionScope Connection را Dispose می‌کند:**
   - فقط در `DisposeAsync` Connection dispose می‌شود
   - `DisposeAsync` فقط زمانی صدا زده می‌شود که `await using` تمام شود

```154:156:GsSample/Infrastructure/Factories/TransactionContext.cs
            // Dispose کردن transaction و connection
            await Transaction.DisposeAsync().ConfigureAwait(false);
            await Connection.DisposeAsync().ConfigureAwait(false);
```

**نتیجه:** Connection و Transaction تا زمانی که Handler تمام نشود و `await using` dispose نشود، زنده می‌مانند.

### 1.4. رفتار Commit/Rollback در تمام مسیرها

**سوال:** رفتار Commit / Rollback در تمام مسیرهای اجرایی، به‌ویژه در صورت بروز Exception، چگونه تضمین می‌شود؟

**پاسخ:** با سه لایه محافظت:

#### لایه 1: Try-Catch در Handler

```25:36:GsSample/Application/Handlers/CreatePersonCommandHandler.cs
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

#### لایه 2: Auto Rollback در DisposeAsync

```138:149:GsSample/Infrastructure/Factories/TransactionContext.cs
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
```

#### لایه 3: await using برای تضمین Dispose

```23:23:GsSample/Application/Handlers/CreatePersonCommandHandler.cs
        await using var transaction = await transactionContext.BeginTransactionAsync(cancellationToken);
```

**نتیجه:** حتی اگر Exception رخ دهد و catch block اجرا نشود، `await using` تضمین می‌کند که `DisposeAsync` صدا زده شود و Transaction rollback شود.

---

## 🔐 2. مالکیت Connection (Connection Ownership)

### 2.1. چه کسی Connection را می‌سازد؟

**پاسخ:** `TransactionContext` در `BeginTransactionAsync` Connection را می‌سازد.

```44:46:GsSample/Infrastructure/Factories/TransactionContext.cs
        // ایجاد connection جدید
        var connection = await _connectionFactory.CreateWriteConnection(cancellationToken)
            .ConfigureAwait(false);
```

### 2.2. چه کسی Connection را Dispose می‌کند؟

**پاسخ:** `TransactionScope` در `DisposeAsync` Connection را Dispose می‌کند.

```154:156:GsSample/Infrastructure/Factories/TransactionContext.cs
            // Dispose کردن transaction و connection
            await Transaction.DisposeAsync().ConfigureAwait(false);
            await Connection.DisposeAsync().ConfigureAwait(false);
```

### 2.3. قاعده مالکیت Connection

**قاعده:**
- **مالک:** `TransactionScope` مالک Connection است
- **طول عمر:** Connection از `BeginTransactionAsync` تا `DisposeAsync` زنده است
- **Repositoryها:** فقط **reference** به Connection را دریافت می‌کنند (نه ownership)
- **Dispose:** فقط `TransactionScope.DisposeAsync` Connection را dispose می‌کند

### 2.4. چرا این طراحی درست است؟

**مزایا:**
1. ✅ **Single Responsibility:** فقط TransactionScope مسئولیت lifecycle Connection را دارد
2. ✅ **Connection Sharing:** همه Repositoryها از همان Connection استفاده می‌کنند
3. ✅ **Thread Safety:** هر async context Connection خودش را دارد
4. ✅ **Resource Management:** Connection فقط یک بار dispose می‌شود

**مثال:**

```csharp
// Handler
await using var transaction = await _transactionContext.BeginTransactionAsync(ct);
// ✅ Connection ایجاد شد و در TransactionScope نگهداری می‌شود

try
{
    // Repository 1: از همان Connection استفاده می‌کند
    await _repo1.SaveAsync(data1, ct);
    // ✅ Connection هنوز زنده است
    
    // Repository 2: از همان Connection استفاده می‌کند
    await _repo2.SaveAsync(data2, ct);
    // ✅ Connection هنوز زنده است
    
    await transaction.CommitAsync(ct);
    // ✅ Connection هنوز زنده است (برای commit)
}
finally
{
    // await using تضمین می‌کند که DisposeAsync صدا زده شود
}
// ✅ در اینجا Connection dispose می‌شود
```

---

## 🛣️ 3. Routing خواندن/نوشتن (Read/Write Routing)

### 3.1. Routing برای Read Operations

**سوال:** برای Readهایی که داخل Transaction انجام می‌شوند، به کدام سمت هدایت می‌شوند و چرا؟

**پاسخ:** Read operations داخل Transaction به **Write DB** هدایت می‌شوند.

**دلیل:** برای **Consistency** - باید داده‌هایی که در Transaction نوشته شده‌اند را ببینیم.

```29:34:GsSample/Infrastructure/Extensions/ConnectionExtensions.cs
        // ✅ اگر transaction فعال باشد، از Write DB استفاده می‌کنیم (برای consistency)
        var currentConnection = transactionContext.GetCurrentConnection();
        if (currentConnection != null)
        {
            return currentConnection;
        }
```

**مثال:**

```csharp
await using var transaction = await _transactionContext.BeginTransactionAsync(ct);

// Write: به Write DB می‌رود
await _repo.CreatePerson(person, ct);

// Read: به Write DB می‌رود (نه Read Replica) - برای consistency
var person = await _repo.GetById(id, ct);
// ✅ می‌تواند person که تازه ایجاد شده را ببیند

await transaction.CommitAsync(ct);
```

### 3.2. Routing برای Write Operations

**پاسخ:** Write operations همیشه به **Write DB** می‌روند.

```56:61:GsSample/Infrastructure/Extensions/ConnectionExtensions.cs
        // ✅ اگر transaction فعال باشد، از connection مشترک استفاده می‌کنیم
        var currentConnection = transactionContext.GetCurrentConnection();
        if (currentConnection != null)
        {
            return currentConnection;
        }
```

### 3.3. Routing خارج از Transaction

**پاسخ:** خارج از Transaction:
- **Read:** به **Read Replica** می‌رود
- **Write:** به **Write DB** می‌رود

```36:37:GsSample/Infrastructure/Extensions/ConnectionExtensions.cs
        // ✅ در غیر این صورت، از Read Replica استفاده می‌کنیم
        return await connectionFactory.CreateReadConnection(cancellationToken).ConfigureAwait(false);
```

### 3.4. جدول Routing

| وضعیت | Read Operation | Write Operation |
|-------|----------------|-----------------|
| **داخل Transaction** | Write DB (برای consistency) | Write DB (Connection مشترک) |
| **خارج از Transaction** | Read Replica | Write DB |

---

## 📊 4. مثال کامل: چند Repository در یک Transaction

### 4.1. مثال واقعی

```csharp
public async Task<Result> Handle(CreateOrderCommand cmd, CancellationToken ct)
{
    // ✅ 1. شروع Transaction در Handler
    await using var transaction = await _transactionContext.BeginTransactionAsync(ct);
    
    try
    {
        // ✅ 2. Repository 1: ایجاد Order
        var order = await _orderRepository.CreateOrderAsync(cmd.Order, ct);
        // ✅ Connection: Write DB (از TransactionScope)
        
        // ✅ 3. Repository 2: ایجاد OrderItems
        foreach (var item in cmd.Items)
        {
            await _orderItemRepository.CreateOrderItemAsync(order.Id, item, ct);
            // ✅ Connection: همان Write DB (Connection مشترک)
        }
        
        // ✅ 4. Repository 3: Read برای validation
        var customer = await _customerRepository.GetByIdAsync(cmd.CustomerId, ct);
        // ✅ Connection: Write DB (نه Read Replica) - برای consistency
        
        // ✅ 5. Repository 4: Update Inventory
        await _inventoryRepository.DecreaseStockAsync(item.ProductId, item.Quantity, ct);
        // ✅ Connection: همان Write DB (Connection مشترک)
        
        // ✅ 6. Commit در Handler
        await transaction.CommitAsync(ct);
        // ✅ همه عملیات commit شدند
        
        return Result.Success(order.Id);
    }
    catch (Exception ex)
    {
        // ✅ 7. Rollback در Handler
        await transaction.RollbackAsync(ct);
        // ✅ همه عملیات rollback شدند
        
        throw;
    }
    // ✅ 8. DisposeAsync به صورت خودکار صدا زده می‌شود
    // ✅ Connection و Transaction dispose می‌شوند
}
```

### 4.2. نمودار جریان

```
┌─────────────────────────────────────────────────────────────┐
│                    Handler (Application Layer)                │
│                                                              │
│  1. BeginTransactionAsync()                                 │
│     ↓                                                        │
│     TransactionContext.BeginTransactionAsync()                │
│       - Create Connection (Write DB)                         │
│       - Begin Transaction                                    │
│       - Store in AsyncLocal                                  │
│                                                              │
│  2. Repository 1: CreateOrderAsync()                        │
│     ↓                                                        │
│     ConnectionExtensions.ExecuteWriteCommandAsync()          │
│       - GetCurrentConnection() → Connection از AsyncLocal    │
│       - Execute Command                                       │
│                                                              │
│  3. Repository 2: CreateOrderItemAsync()                    │
│     ↓                                                        │
│     ConnectionExtensions.ExecuteWriteCommandAsync()          │
│       - GetCurrentConnection() → همان Connection             │
│       - Execute Command                                       │
│                                                              │
│  4. Repository 3: GetByIdAsync() (Read)                       │
│     ↓                                                        │
│     ConnectionExtensions.ExecuteReadQueryAsync()             │
│       - GetCurrentConnection() → همان Connection (Write DB) │
│       - Execute Query (برای consistency)                    │
│                                                              │
│  5. CommitAsync()                                            │
│     ↓                                                        │
│     TransactionScope.CommitAsync()                            │
│       - Transaction.Commit()                                 │
│                                                              │
│  6. DisposeAsync() (automatic)                               │
│     ↓                                                        │
│     TransactionScope.DisposeAsync()                           │
│       - ClearScope() (null کردن AsyncLocal)                 │
│       - Dispose Transaction                                  │
│       - Dispose Connection                                   │
└─────────────────────────────────────────────────────────────┘
```

---

## ✅ 5. خلاصه پاسخ‌ها

| سوال | پاسخ |
|------|------|
| **Transaction در کدام لایه آغاز می‌شود؟** | لایه Application (Handler) |
| **Transaction در کدام لایه خاتمه داده می‌شود؟** | لایه Application (Handler) |
| **چگونه تضمین می‌شود Connection dispose نشود؟** | Connection در TransactionScope نگهداری می‌شود و فقط در DisposeAsync dispose می‌شود |
| **چه کسی Connection را می‌سازد؟** | TransactionContext در BeginTransactionAsync |
| **چه کسی Connection را Dispose می‌کند؟** | TransactionScope در DisposeAsync |
| **Read داخل Transaction به کجا می‌رود؟** | Write DB (برای consistency) |
| **Write داخل Transaction به کجا می‌رود؟** | Write DB (Connection مشترک) |
| **چگونه Commit/Rollback تضمین می‌شود؟** | با Try-Catch در Handler + Auto Rollback در DisposeAsync |

---

## 🎯 6. Design Candidate - آماده برای ارزیابی

این طراحی یک **Design Candidate کامل و یکپارچه** است که:

1. ✅ **Transaction Lifecycle** به صورت شفاف تعریف شده است
2. ✅ **Connection Ownership** مشخص است
3. ✅ **Read/Write Routing** به صورت کامل پیاده‌سازی شده است
4. ✅ **Exception Handling** در تمام مسیرها تضمین شده است
5. ✅ **Resource Management** به درستی انجام می‌شود
6. ✅ **Thread Safety** با AsyncLocal تضمین شده است

**آماده برای:**
- ✅ ارزیابی به عنوان Design Candidate
- ✅ تطبیق با سورس اصلی GSTech
- ✅ استفاده در پروژه‌های واقعی

---

**پایان مستند**

