# 🔧 راهنمای رفع مشکل دیتابیس خالی

اگر دیتابیس `GsSample` ایجاد شده ولی جدول یا داده ندارد، از یکی از روش‌های زیر استفاده کنید:

---

## 🎯 روش 1: اجرای Script SQL (سریع‌ترین روش)

### در SQL Server Management Studio:

1. به SQL Server متصل شوید (User: `ETL`, Password: `123456`)
2. فایل `Infrastructure/Database/SetupDatabase.sql` را باز کنید
3. کل script را اجرا کنید (F5)

**این script:**
- ✅ جدول `Persons` را ایجاد می‌کند
- ✅ 10 رکورد نمونه insert می‌کند
- ✅ نتایج را نمایش می‌دهد

---

## 🎯 روش 2: اجرای دستورات SQL مستقیم

در SQL Server Management Studio:

```sql
USE GsSample;
GO

-- ایجاد جدول
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Persons]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Persons] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [FirstName] NVARCHAR(100) NOT NULL,
        [LastName] NVARCHAR(100) NOT NULL,
        [DateOfBirth] DATETIME2 NOT NULL,
        [CreatedAt] DATETIME2 DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 DEFAULT GETUTCDATE()
    );
    
    CREATE INDEX IX_Persons_LastName ON [dbo].[Persons]([LastName]);
    CREATE INDEX IX_Persons_DateOfBirth ON [dbo].[Persons]([DateOfBirth]);
    
    PRINT 'Table created!';
END
GO

-- Insert داده‌های نمونه
IF (SELECT COUNT(*) FROM Persons) = 0
BEGIN
    INSERT INTO Persons (FirstName, LastName, DateOfBirth) VALUES
    (N'علی', N'محمدی', '1990-01-15'),
    (N'فاطمه', N'احمدی', '1992-05-20'),
    (N'محمد', N'رضایی', '1988-08-10'),
    (N'زهرا', N'کریمی', '1995-03-25'),
    (N'حسین', N'نوری', '1991-11-05'),
    (N'مریم', N'حسینی', '1993-07-12'),
    (N'رضا', N'موسوی', '1989-09-18'),
    (N'سارا', N'جعفری', '1994-04-30'),
    (N'امیر', N'کاظمی', '1996-06-08'),
    (N'نرگس', N'صادقی', '1997-02-14');
    
    PRINT 'Sample data inserted!';
END
GO

-- نمایش داده‌ها
SELECT * FROM Persons;
GO
```

---

## 🎯 روش 3: اجرای برنامه (خودکار)

```bash
cd API
dotnet run
```

برنامه به صورت خودکار:
1. ✅ جدول را ایجاد می‌کند (اگر وجود نداشته باشد)
2. ✅ داده‌های نمونه را insert می‌کند (اگر جدول خالی باشد)

---

## 🔍 بررسی وضعیت دیتابیس

برای بررسی اینکه چه چیزی در دیتابیس وجود دارد:

```sql
USE GsSample;
GO

-- بررسی وجود جدول
SELECT 
    TABLE_NAME,
    TABLE_SCHEMA
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE';

-- بررسی تعداد رکوردها
SELECT 
    t.name AS TableName,
    p.rows AS RowCount
FROM sys.tables t
INNER JOIN sys.partitions p ON t.object_id = p.object_id
WHERE t.name = 'Persons' AND p.index_id IN (0,1)
GROUP BY t.name, p.rows;

-- مشاهده ساختار جدول
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Persons';
```

---

## ✅ بعد از اجرای Script

بعد از اجرای script، باید ببینید:

```
✅ Table Persons created successfully!
✅ Sample data inserted successfully!
Total records: 10
```

و در query result:
- 10 رکورد با نام‌های فارسی
- هر رکورد دارای Id, FirstName, LastName, DateOfBirth

---

## 🎯 تست سریع

بعد از ایجاد جدول و داده‌ها:

```sql
USE GsSample;
GO

-- شمارش
SELECT COUNT(*) AS Total FROM Persons;

-- مشاهده همه
SELECT * FROM Persons ORDER BY Id;

-- جستجو
SELECT * FROM Persons WHERE FirstName LIKE N'علی%';
```

---

**نکته:** اگر هنوز مشکل دارید، لطفاً خطای دقیق را بفرستید تا بتوانم کمک کنم.

