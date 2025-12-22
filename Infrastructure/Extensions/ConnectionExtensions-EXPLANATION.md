# چرا متدهای ConnectionExtensions Static هستند؟

## سوال:
چرا متدهای `GetReadConnectionAsync` و `GetWriteConnectionAsync` در کلاس `ConnectionExtensions` به صورت `static` تعریف شده‌اند؟

## پاسخ:

### 1. **Extension Methods Pattern**
متدهای `static` در یک کلاس `static` برای پیاده‌سازی **Extension Methods** استفاده می‌شوند. این الگو در C# بسیار رایج است:

```csharp
public static class ConnectionExtensions
{
    public static async Task<DbConnection> GetReadConnectionAsync(...)
    {
        // ...
    }
}
```

### 2. **Utility Methods (Helper Functions)**
این متدها **Utility Methods** هستند که:
- **State ندارند**: هیچ state یا data member ندارند
- **Pure Functions**: فقط بر اساس input، output تولید می‌کنند
- **Reusable**: در همه جا قابل استفاده هستند

### 3. **نیاز به Instance نیست**
این متدها:
- به هیچ instance خاصی نیاز ندارند
- فقط از پارامترهای ورودی استفاده می‌کنند
- می‌توانند مستقل از هر object خاصی کار کنند

### 4. **مزایای Static Methods**

#### ✅ **Performance**
- نیازی به ایجاد instance نیست
- Overhead کمتر

#### ✅ **Memory**
- هیچ object در memory ایجاد نمی‌شود
- فقط متدها در memory هستند

#### ✅ **Thread-Safety**
- چون state ندارند، thread-safe هستند
- می‌توانند همزمان از چند thread صدا زده شوند

#### ✅ **سادگی استفاده**
```csharp
// ✅ ساده و مستقیم
await ConnectionExtensions.GetReadConnectionAsync(...);

// ❌ اگر instance بود، باید این کار را می‌کردیم:
var helper = new ConnectionExtensions();
await helper.GetReadConnectionAsync(...);
```

### 5. **مقایسه با Instance Methods**

#### اگر Instance بود:
```csharp
public class ConnectionHelper
{
    private readonly IConnectionFactory _factory;
    private readonly ITransactionContext _context;
    
    public ConnectionHelper(IConnectionFactory factory, ITransactionContext context)
    {
        _factory = factory;
        _context = context;
    }
    
    public async Task<DbConnection> GetReadConnectionAsync(...)
    {
        // استفاده از _factory و _context
    }
}
```

**مشکلات:**
- ❌ باید در DI ثبت شود
- ❌ باید در constructor inject شود
- ❌ برای هر استفاده، باید instance بسازیم
- ❌ Memory overhead بیشتر

#### با Static:
```csharp
public static class ConnectionExtensions
{
    public static async Task<DbConnection> GetReadConnectionAsync(
        IConnectionFactory factory,
        ITransactionContext context,
        ...)
    {
        // استفاده از factory و context که به عنوان parameter می‌آیند
    }
}
```

**مزایا:**
- ✅ نیازی به DI registration نیست
- ✅ نیازی به constructor injection نیست
- ✅ مستقیماً قابل استفاده است
- ✅ Memory overhead کمتر

### 6. **استفاده در Repository**

```csharp
public class PersonRepository
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ITransactionContext _transactionContext;
    
    public async Task<Person> GetById(int id, CancellationToken ct)
    {
        // ✅ استفاده مستقیم از static method
        return await ConnectionExtensions.ExecuteReadQueryAsync(
            _connectionFactory,
            _transactionContext,
            async (db, ct) => { /* ... */ },
            ct);
    }
}
```

### 7. **نتیجه‌گیری**

متدهای `static` برای این Helper Methods مناسب هستند چون:
1. **State ندارند**
2. **Pure Functions هستند**
3. **Performance بهتر**
4. **Memory efficient**
5. **Thread-safe**
6. **ساده‌تر استفاده می‌شوند**

این الگو در .NET بسیار رایج است (مثل `System.Linq.Enumerable` که همه متدهایش static هستند).

