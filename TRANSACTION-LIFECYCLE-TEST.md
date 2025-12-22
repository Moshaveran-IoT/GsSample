# تست Lifecycle Transaction

این فایل توضیح می‌دهد که چگونه lifecycle یک Transaction را تست کنیم.

## 📋 تست‌های موجود

کلاس `TestTransactionLifecycle` شامل 6 تست کامل است:

### 1. تست Transaction ساده - Commit موفق
- شروع Transaction
- ایجاد Person در Transaction
- بررسی Connection Sharing
- Commit Transaction
- بررسی نهایی در دیتابیس

### 2. تست Transaction با Rollback
- شروع Transaction
- ایجاد Person
- Rollback Transaction
- بررسی که Person ایجاد نشده

### 3. تست Transaction با چند عملیات
- شروع Transaction
- Create → Read → Update → Read
- Commit همه عملیات
- بررسی نهایی

### 4. تست Transaction با Exception (Auto Rollback)
- شروع Transaction
- ایجاد Person
- ایجاد Exception
- بررسی Auto Rollback توسط DisposeAsync

### 5. تست Connection Sharing
- بررسی که همه Repositoryها از همان Connection استفاده می‌کنند
- بررسی Connection State
- بررسی Connection بعد از Commit

### 6. تست Read/Write Routing در Transaction
- Read در Transaction (باید به Write DB برود)
- Write در Transaction (باید از همان Connection استفاده کند)
- Read خارج از Transaction (باید به Read Replica برود)

## 🚀 نحوه اجرا

### روش 1: اجرای خودکار (در Program.cs)

تست به صورت خودکار در `Program.cs` اجرا می‌شود:

```bash
cd API
dotnet run
```

### روش 2: اجرای دستی

```csharp
var configuration = // ... load configuration
var logger = // ... create logger

var success = await TestTransactionLifecycle.RunTransactionLifecycleTestAsync(
    configuration,
    logger,
    CancellationToken.None);
```

## 📊 خروجی تست

تست‌ها خروجی مفصلی تولید می‌کنند که شامل:

- ✅ وضعیت Transaction (شروع، Commit، Rollback)
- ✅ Connection State و Sharing
- ✅ عملیات انجام شده در Transaction
- ✅ بررسی نهایی در دیتابیس
- ✅ Read/Write Routing

## 🔍 نکات مهم

1. **Connection Sharing**: در Transaction، همه Repositoryها از همان Connection استفاده می‌کنند
2. **Read/Write Routing**: در Transaction، Read هم به Write DB می‌رود (برای consistency)
3. **Auto Rollback**: اگر Transaction commit نشود، DisposeAsync به صورت خودکار rollback می‌کند
4. **Thread Safety**: هر async context Transaction خودش را دارد (با استفاده از AsyncLocal)

## 📝 مثال خروجی

```
🔄 Starting Transaction Lifecycle Test...

📋 Test 1: Simple Transaction - Successful Commit
─────────────────────────────────────────────────
  → Starting transaction...
  ✅ Transaction started
  → Connection: Open
  → Transaction: ReadCommitted
  → Creating Person in transaction...
  ✅ Person created with ID: 1
  → Current connection in context: True
  → Connection is same: True
  → Committing transaction...
  ✅ Transaction committed successfully
  → Connection after commit: False
  ✅ Person retrieved from database: Transaction Test
  ✅ Test data cleaned up
```

## ✅ بررسی صحت تست

برای اطمینان از صحت تست، بررسی کنید:

1. ✅ Transaction شروع می‌شود
2. ✅ Connection در Context موجود است
3. ✅ عملیات در Transaction انجام می‌شود
4. ✅ Commit/Rollback به درستی کار می‌کند
5. ✅ Connection بعد از Commit از Context حذف می‌شود
6. ✅ Read/Write Routing به درستی کار می‌کند

