-- Script برای ایجاد جدول Persons
-- این script باید در دیتابیس اجرا شود

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

