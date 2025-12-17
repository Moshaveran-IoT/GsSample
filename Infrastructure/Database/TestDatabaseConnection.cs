using System.Data;
using System.Data.Common;
using Dapper;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Database;

/// <summary>
/// کلاس کمکی برای تست اتصال به دیتابیس و ایجاد جدول
/// </summary>
public static class TestDatabaseConnection
{
    /// <summary>
    /// تست اتصال به دیتابیس و ایجاد جدول Persons در صورت عدم وجود
    /// </summary>
    public static async Task<bool> TestAndSetupDatabaseAsync(
        IConfiguration configuration,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = configuration.GetConnectionString("ApplicationConnection")
                ?? throw new InvalidOperationException("ApplicationConnection connection string is required.");

            logger?.LogInformation("Testing database connection...");
            logger?.LogInformation("Connection String: {ConnectionString}", 
                connectionString.Replace("Password=123456", "Password=***"));

            // استخراج نام دیتابیس
            var databaseName = ExtractDatabaseName(connectionString);
            var masterConnectionString = GetMasterConnectionString(connectionString);
            
            // اتصال به master برای بررسی و ایجاد دیتابیس
            await using var masterConnection = new Microsoft.Data.SqlClient.SqlConnection(masterConnectionString);
            await masterConnection.OpenAsync(cancellationToken);
            logger?.LogInformation("✅ Connected to master database");

            // بررسی وجود دیتابیس
            var dbExists = await CheckDatabaseExistsAsync(masterConnection, databaseName, cancellationToken);
            
            if (!dbExists)
            {
                logger?.LogWarning("⚠️ Database '{DatabaseName}' does not exist. Creating...", databaseName);
                await CreateDatabaseAsync(masterConnection, databaseName, cancellationToken);
                logger?.LogInformation("✅ Database '{DatabaseName}' created successfully!", databaseName);
            }
            else
            {
                logger?.LogInformation("✅ Database '{DatabaseName}' exists.", databaseName);
            }

            // اتصال به دیتابیس هدف
            // اگر دیتابیس تازه ایجاد شده باشد، ممکن است نیاز به کمی تأخیر باشد
            if (!dbExists)
            {
                logger?.LogInformation("⏳ Waiting for database to be ready...");
                await Task.Delay(500, cancellationToken); // تأخیر کوتاه برای اطمینان از آماده بودن دیتابیس
            }

            await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            
            // تلاش برای اتصال (با retry در صورت نیاز)
            var maxRetries = 3;
            var retryCount = 0;
            while (retryCount < maxRetries)
            {
                try
                {
                    await connection.OpenAsync(cancellationToken);
                    logger?.LogInformation("✅ Database connection successful!");
                    break;
                }
                catch (Exception ex) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    logger?.LogWarning("⚠️ Connection attempt {Attempt} failed. Retrying... ({Error})", 
                        retryCount, ex.Message);
                    await Task.Delay(1000 * retryCount, cancellationToken); // تأخیر افزایشی
                }
            }

            // بررسی و ایجاد جدول Persons
            var tableExists = await CheckTableExistsAsync(connection, "Persons", cancellationToken);
            
            if (!tableExists)
            {
                logger?.LogWarning("⚠️ Table 'Persons' does not exist. Creating...");
                await CreatePersonsTableAsync(connection, cancellationToken);
                logger?.LogInformation("✅ Table 'Persons' created successfully!");
            }
            else
            {
                logger?.LogInformation("✅ Table 'Persons' exists.");
            }

            // تست یک query ساده
            var count = await connection.QuerySingleAsync<int>(
                new CommandDefinition("SELECT COUNT(*) FROM Persons", cancellationToken: cancellationToken));
            logger?.LogInformation("✅ Current record count in Persons table: {Count}", count);

            // درج داده‌های نمونه در صورت خالی بودن جدول
            if (count == 0)
            {
                logger?.LogInformation("📝 Inserting sample data...");
                await InsertSampleDataAsync(connection, cancellationToken);
                var newCount = await connection.QuerySingleAsync<int>(
                    new CommandDefinition("SELECT COUNT(*) FROM Persons", cancellationToken: cancellationToken));
                logger?.LogInformation("✅ Sample data inserted! Total records: {Count}", newCount);
            }
            else
            {
                logger?.LogInformation("ℹ️ Table already contains data. Skipping sample data insertion.");
            }

            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "❌ Database test failed: {Message}", ex.Message);
            return false;
        }
    }

    private static string ExtractDatabaseName(string connectionString)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        return builder.InitialCatalog;
    }

    private static string GetMasterConnectionString(string connectionString)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        builder.InitialCatalog = "master";
        return builder.ConnectionString;
    }

    private static async Task<bool> CheckDatabaseExistsAsync(
        DbConnection connection, 
        string databaseName, 
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT COUNT(*) 
            FROM sys.databases 
            WHERE name = @DatabaseName";
        
        var command = new CommandDefinition(sql, new { DatabaseName = databaseName }, cancellationToken: cancellationToken);
        var count = await connection.QuerySingleAsync<int>(command);
        return count > 0;
    }

    private static async Task CreateDatabaseAsync(
        DbConnection connection, 
        string databaseName, 
        CancellationToken cancellationToken)
    {
        // Escape کردن نام دیتابیس برای جلوگیری از SQL Injection
        var escapedName = databaseName.Replace("]", "]]");
        var sql = $@"
            IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{escapedName}')
            BEGIN
                CREATE DATABASE [{escapedName}];
            END";
        
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    private static async Task<bool> CheckTableExistsAsync(
        DbConnection connection, 
        string tableName, 
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TableName";
        
        var command = new CommandDefinition(sql, new { TableName = tableName }, cancellationToken: cancellationToken);
        var count = await connection.QuerySingleAsync<int>(command);
        return count > 0;
    }

    private static async Task CreatePersonsTableAsync(
        DbConnection connection, 
        CancellationToken cancellationToken)
    {
        var sql = @"
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
            END";
        
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    /// <summary>
    /// درج داده‌های نمونه در جدول Persons
    /// </summary>
    private static async Task InsertSampleDataAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var sampleData = new[]
        {
            new { FirstName = "علی", LastName = "محمدی", DateOfBirth = new DateTime(1990, 1, 15) },
            new { FirstName = "فاطمه", LastName = "احمدی", DateOfBirth = new DateTime(1992, 5, 20) },
            new { FirstName = "محمد", LastName = "رضایی", DateOfBirth = new DateTime(1988, 8, 10) },
            new { FirstName = "زهرا", LastName = "کریمی", DateOfBirth = new DateTime(1995, 3, 25) },
            new { FirstName = "حسین", LastName = "نوری", DateOfBirth = new DateTime(1991, 11, 5) },
            new { FirstName = "مریم", LastName = "حسینی", DateOfBirth = new DateTime(1993, 7, 12) },
            new { FirstName = "رضا", LastName = "موسوی", DateOfBirth = new DateTime(1989, 9, 18) },
            new { FirstName = "سارا", LastName = "جعفری", DateOfBirth = new DateTime(1994, 4, 30) },
            new { FirstName = "امیر", LastName = "کاظمی", DateOfBirth = new DateTime(1996, 6, 8) },
            new { FirstName = "نرگس", LastName = "صادقی", DateOfBirth = new DateTime(1997, 2, 14) }
        };

        var sql = @"
            INSERT INTO Persons (FirstName, LastName, DateOfBirth)
            VALUES (@FirstName, @LastName, @DateOfBirth)";

        foreach (var person in sampleData)
        {
            var command = new CommandDefinition(sql, person, cancellationToken: cancellationToken);
            await connection.ExecuteAsync(command);
        }
    }
}

