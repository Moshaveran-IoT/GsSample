-- Script برای درج داده‌های نمونه در جدول Persons
-- این script می‌تواند به صورت دستی اجرا شود یا توسط برنامه به صورت خودکار

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
END
ELSE
BEGIN
    PRINT 'Table already contains data. Skipping sample data insertion.';
END
GO

