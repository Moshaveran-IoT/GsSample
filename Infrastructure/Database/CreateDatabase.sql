-- Script برای ایجاد دیتابیس GsSample
-- این script می‌تواند به صورت دستی در SQL Server Management Studio اجرا شود

-- بررسی و ایجاد دیتابیس
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'GsSample')
BEGIN
    CREATE DATABASE [GsSample];
    PRINT 'Database GsSample created successfully!';
END
ELSE
BEGIN
    PRINT 'Database GsSample already exists.';
END
GO

-- استفاده از دیتابیس
USE [GsSample];
GO

-- ایجاد جدول Persons
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
    
    -- ایجاد Index برای بهبود performance
    CREATE INDEX IX_Persons_LastName ON [dbo].[Persons]([LastName]);
    CREATE INDEX IX_Persons_DateOfBirth ON [dbo].[Persons]([DateOfBirth]);
    
    PRINT 'Table Persons created successfully.';
END
ELSE
BEGIN
    PRINT 'Table Persons already exists.';
END
GO

-- درج داده‌های نمونه (فقط در صورت خالی بودن جدول)
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
    
    PRINT 'Sample data inserted successfully!';
    PRINT 'Total records: ' + CAST((SELECT COUNT(*) FROM Persons) AS VARCHAR);
END
ELSE
BEGIN
    PRINT 'Table already contains data. Skipping sample data insertion.';
    PRINT 'Total records: ' + CAST((SELECT COUNT(*) FROM Persons) AS VARCHAR);
END
GO

