-- ============================================================
-- Script کامل برای راه‌اندازی دیتابیس GsSample
-- این script را در SQL Server Management Studio اجرا کنید
-- ============================================================

USE [GsSample];
GO

-- ============================================================
-- ایجاد جدول Persons
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Persons]') AND type in (N'U'))
BEGIN
    PRINT 'Creating table Persons...';
    
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
    
    PRINT '✅ Table Persons created successfully!';
END
ELSE
BEGIN
    PRINT 'ℹ️ Table Persons already exists.';
END
GO

-- ============================================================
-- درج داده‌های نمونه
-- ============================================================
DECLARE @RecordCount INT;
SELECT @RecordCount = COUNT(*) FROM Persons;

IF @RecordCount = 0
BEGIN
    PRINT 'Inserting sample data...';
    
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
    
    SELECT @RecordCount = COUNT(*) FROM Persons;
    PRINT '✅ Sample data inserted successfully!';
    PRINT 'Total records: ' + CAST(@RecordCount AS VARCHAR);
END
ELSE
BEGIN
    PRINT 'ℹ️ Table already contains data. Skipping sample data insertion.';
    PRINT 'Total records: ' + CAST(@RecordCount AS VARCHAR);
END
GO

-- ============================================================
-- نمایش داده‌ها
-- ============================================================
PRINT '';
PRINT '============================================================';
PRINT 'Database Setup Complete!';
PRINT '============================================================';
PRINT '';

SELECT 
    Id,
    FirstName,
    LastName,
    DateOfBirth,
    CreatedAt
FROM Persons
ORDER BY Id;

PRINT '';
PRINT 'Total records: ' + CAST((SELECT COUNT(*) FROM Persons) AS VARCHAR);
GO

