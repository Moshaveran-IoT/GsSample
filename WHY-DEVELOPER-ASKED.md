# چرا دولوپر این سوالات را مطرح کرد؟

این سند توضیح می‌دهد که چرا دولوپر این سوالات را مطرح کرده و تفاوت بین کد قدیمی (`GsTechV2Backend`) و کد جدید (`GsSample`) چیست.

---

## 🔍 مشکل اصلی

دولوپر در مورد **کد قدیمی در `GsTechV2Backend`** صحبت می‌کند، نه `GsSample`.

### کد قدیمی در `GsTechV2Backend` ❌

**فایل:** `GsTechV2Backend/Shared/SharedDAL/Infrastructure/ConnectionFactory.cs`

```csharp
// ❌ کد قدیمی - مشکل‌دار
public sealed class ConnectionFactory : IConnectionFactory
{
    private readonly Lock _lock = new(); // ❌ خط 11
    private SqlConnection _cachedConnection; // ❌ خط 12

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
                lock (this._lock) // ❌ خط 26 - lock
                {
                    this._cachedConnection = newConn;
                }
            }
            
            return newConn;
        }

        // ❌ استفاده از .Result در lock (deadlock!)
        lock (this._lock) // ❌ خط 37
        {
            if (this._cachedConnection == null)
            {
                // ❌ خط 41: استفاده از .Result در lock → deadlock!
                this._cachedConnection = CreateAndOpenConnectionAsync(...).Result;
            }
            else if (this._cachedConnection.State != ConnectionState.Open)
            {
                this.DisposeCachedConnection();
                // ❌ خط 46: استفاده از .Result در lock → deadlock!
                this._cachedConnection = CreateAndOpenConnectionAsync(...).Result;
            }
            return this._cachedConnection;
        }
    }
}
```

**مشکلات کد قدیمی:**
1. ❌ **استفاده از `lock`** (خطوط 26, 37, 67, 79)
2. ❌ **استفاده از `.Result` در `lock`** (خطوط 41, 46) → **deadlock حتمی**
3. ❌ **Cache کردن Connection** (`_cachedConnection`) → thread-safety مشکل دارد
4. ❌ **Stateful** → مشکلات concurrent access
5. ❌ **Parameters اضافی** (`createNew`, `cacheNewConnection`) → error-prone

---

## ✅ کد جدید در `GsSample`

**فایل:** `GsSample/Infrastructure/Factories/ConnectionFactory.cs`

```csharp
// ✅ کد جدید - بدون مشکل
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

**مزایای کد جدید:**
1. ✅ **بدون `lock`** → بدون deadlock
2. ✅ **بدون `.Result`** → کاملاً async/await
3. ✅ **بدون Cache** → stateless
4. ✅ **Thread-safe** → هر thread Connection خودش را می‌گیرد
5. ✅ **Interface ساده** → بدون parameters اضافی

---

## 🤔 چرا دولوپر این سوالات را مطرح کرد؟

### 1. مشکل فنی: `await` داخل `lock`

**سوال دولوپر:**
> در C# استفاده از `await` داخل یک بلاک `lock` اصولاً مجاز نیست و کامپایلر خطای CS1996 می‌دهد.

**وضعیت در کد قدیمی (`GsTechV2Backend`):**
- ❌ استفاده از `.Result` در `lock` (خطوط 41, 46)
- ❌ این می‌تواند deadlock ایجاد کند

**وضعیت در کد جدید (`GsSample`):**
- ✅ هیچ `lock` استفاده نشده
- ✅ کاملاً async/await

### 2. مشکل معماری: Transaction مشترک

**سوال دولوپر:**
> در بخش‌هایی از لایه Application چند متد و چند Repository باید در چارچوب یک واحد کاری (TransactionScope یا Transaction مشترک) اجرا شوند.
> 
> از آن‌جایی که هر transaction فقط روی یک connection مشخص عمل می‌کند، تا وقتی در همان scope هستیم باید از یک connection مشترک استفاده کنیم.

**وضعیت در کد قدیمی (`GsTechV2Backend`):**
- ❌ برای Transaction مشترک، باید `createNew: false` را به همه Repositoryها بفرستیم
- ❌ اگر یک Repository `createNew: true` بفرستد، transaction می‌شکند
- ❌ Error-prone و thread-safe نیست

**وضعیت در کد جدید (`GsSample`):**
- ✅ از `TransactionContext` با `AsyncLocal` استفاده می‌کند
- ✅ Repositoryها به صورت خودکار از Connection مشترک استفاده می‌کنند
- ✅ ساده و thread-safe

---

## 📊 مقایسه: کد قدیمی vs کد جدید

| مورد | کد قدیمی (`GsTechV2Backend`) | کد جدید (`GsSample`) |
|------|------------------------------|----------------------|
| **`lock`** | ❌ استفاده شده (خطوط 26, 37, 67, 79) | ✅ استفاده نشده |
| **`.Result`** | ❌ استفاده شده در `lock` (خطوط 41, 46) | ✅ استفاده نشده |
| **Cache** | ❌ `_cachedConnection` استفاده شده | ✅ استفاده نشده |
| **Stateful** | ❌ Stateful | ✅ Stateless |
| **Transaction مشترک** | ❌ با `createNew: false` (error-prone) | ✅ با `AsyncLocal` (خودکار) |
| **Thread-Safe** | ❌ مشکل دارد | ✅ Thread-safe |
| **Deadlock** | ❌ امکان deadlock | ✅ بدون deadlock |

---

## 🎯 نتیجه‌گیری

### چرا دولوپر این سوالات را مطرح کرد؟

1. **کد قدیمی در `GsTechV2Backend` مشکلات دارد:**
   - استفاده از `lock` با `.Result` → deadlock
   - Cache کردن Connection → thread-safety مشکل
   - نیاز به Transaction مشترک → error-prone

2. **دولوپر می‌خواهد راه‌حل عملیاتی:**
   - چگونه `await` داخل `lock` را حل کنیم؟
   - چگونه Transaction مشترک را بدون cache پیاده‌سازی کنیم؟

3. **`GsSample` راه‌حل را ارائه می‌دهد:**
   - ✅ حذف `lock` از `ConnectionFactory`
   - ✅ استفاده از `AsyncLocal` در `TransactionContext`
   - ✅ Transaction مشترک بدون cache

---

## 📚 فایل‌های مرتبط

### کد قدیمی (مشکل‌دار):
- `GsTechV2Backend/Shared/SharedDAL/Infrastructure/ConnectionFactory.cs` ❌

### کد جدید (بدون مشکل):
- `GsSample/Infrastructure/Factories/ConnectionFactory.cs` ✅
- `GsSample/Infrastructure/Factories/TransactionContext.cs` ✅

### راهنمای Migration:
- `GsTechV2Backend/ConnectionFactory-Solution/15-DEVELOPER-LOCK-AWAIT-SOLUTION.md` ✅

---

**خلاصه:**
- دولوپر در مورد **کد قدیمی در `GsTechV2Backend`** صحبت می‌کند که مشکلات دارد
- `GsSample` یک **نمونه جدید** است که همه مشکلات را حل کرده
- راه‌حل در `GsSample` پیاده‌سازی شده و می‌تواند به `GsTechV2Backend` migrate شود

