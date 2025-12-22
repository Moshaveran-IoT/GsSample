# 🚀 راهنمای سریع شروع

## ✅ تنظیمات انجام شده

- ✅ Connection String: `localhost`, User: `ETL`, Password: `123456`
- ✅ دیتابیس: `GsSample` (به صورت خودکار ایجاد می‌شود)
- ✅ جدول: `Persons` (به صورت خودکار ایجاد می‌شود)
- ✅ داده‌های نمونه: 10 رکورد (به صورت خودکار insert می‌شود)

---

## 🎯 روش 1: اجرای خودکار (توصیه می‌شود)

```bash
cd API
dotnet run
```

**چه اتفاقی می‌افتد:**
1. ✅ اتصال به SQL Server
2. ✅ ایجاد دیتابیس `GsSample` (اگر وجود نداشته باشد)
3. ✅ ایجاد جدول `Persons` (اگر وجود نداشته باشد)
4. ✅ Insert 10 رکورد نمونه
5. ✅ اجرای تست‌های کامل
6. ✅ راه‌اندازی API

---

## 🎯 روش 2: ایجاد دستی دیتابیس

اگر می‌خواهید دیتابیس را دستی ایجاد کنید:

### در SQL Server Management Studio:

1. به SQL Server متصل شوید (User: `ETL`, Password: `123456`)
2. فایل `Infrastructure/Database/CreateDatabase.sql` را باز کنید
3. کل script را اجرا کنید (F5)

**یا** دستورات زیر را اجرا کنید:

```sql
-- ایجاد دیتابیس
CREATE DATABASE GsSample;
GO

USE GsSample;
GO

-- ایجاد جدول
CREATE TABLE [dbo].[Persons] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [FirstName] NVARCHAR(100) NOT NULL,
    [LastName] NVARCHAR(100) NOT NULL,
    [DateOfBirth] DATETIME2 NOT NULL,
    [CreatedAt] DATETIME2 DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2 DEFAULT GETUTCDATE()
);
GO
```

---

## 🔍 بررسی دیتابیس

بعد از اجرا، در SQL Server Management Studio:

```sql
USE GsSample;
GO

-- مشاهده همه داده‌ها
SELECT * FROM Persons;

-- شمارش
SELECT COUNT(*) AS TotalPersons FROM Persons;

-- مشاهده ساختار
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Persons';
```

---

## 🧪 تست API

بعد از راه‌اندازی API، می‌توانید تست کنید:

### دریافت همه Persons:
```http
GET http://localhost:5000/api/Person
```

### دریافت Person با ID:
```http
GET http://localhost:5000/api/Person/1
```

### ایجاد Person جدید:
```http
POST http://localhost:5000/api/Person
Content-Type: application/json

{
  "firstName": "تست",
  "lastName": "کاربر",
  "dateOfBirth": "2000-01-01T00:00:00Z"
}
```

---

## ❌ عیب‌یابی

### مشکل: "Cannot open database"

**راه‌حل:**
1. مطمئن شوید SQL Server در حال اجرا است
2. User `ETL` باید دسترسی CREATE DATABASE داشته باشد
3. یا دیتابیس را دستی ایجاد کنید (روش 2)

### مشکل: "Login failed"

**راه‌حل:**
1. رمز عبور را بررسی کنید
2. User `ETL` باید در SQL Server وجود داشته باشد
3. SQL Server Authentication باید فعال باشد

### مشکل: "Database already exists"

**راه‌حل:**
این یک warning است و مشکلی ایجاد نمی‌کند. دیتابیس از قبل وجود دارد.

---

## 📊 داده‌های نمونه

10 رکورد با نام‌های فارسی:
- علی محمدی
- فاطمه احمدی
- محمد رضایی
- زهرا کریمی
- حسین نوری
- مریم حسینی
- رضا موسوی
- سارا جعفری
- امیر کاظمی
- نرگس صادقی

---

## ✅ چک‌لیست

- [ ] SQL Server در حال اجرا است
- [ ] User `ETL` با Password `123456` وجود دارد
- [ ] User `ETL` دسترسی CREATE DATABASE دارد
- [ ] Connection String در `appsettings.json` صحیح است
- [ ] پروژه Build می‌شود

---

**همه چیز آماده است! 🎉**

