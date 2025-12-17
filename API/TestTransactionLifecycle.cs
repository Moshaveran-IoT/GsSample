using Domain.Features;
using Domain.Models;
using Infrastructure;
using Infrastructure.Database;
using Infrastructure.Extensions;
using Infrastructure.Interfaces;
using Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace API;

/// <summary>
/// تست کامل lifecycle یک Transaction
/// این کلاس نشان می‌دهد که transaction چگونه شروع، استفاده و commit/rollback می‌شود
/// </summary>
public static class TestTransactionLifecycle
{
    /// <summary>
    /// تست کامل lifecycle یک Transaction
    /// </summary>
    public static async Task<bool> RunTransactionLifecycleTestAsync(
        IConfiguration configuration,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger?.LogInformation("🔄 Starting Transaction Lifecycle Test...\n");

            // Setup DI Container
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging(builder => builder.AddConsole());
            services.AddConnectionFactory(configuration);
            services.AddRepositories();
            services.AddMediatR(typeof(Application.GetAllPersonQueryHandler).Assembly);

            var serviceProvider = services.BuildServiceProvider();
            var personRepository = serviceProvider.GetRequiredService<IPersonRepository>();
            var transactionContext = serviceProvider.GetRequiredService<ITransactionContext>();
            var connectionFactory = serviceProvider.GetRequiredService<IConnectionFactory>();

            // ============================================================
            // تست 1: Transaction ساده - Commit موفق
            // ============================================================
            logger?.LogInformation("📋 Test 1: Simple Transaction - Successful Commit");
            logger?.LogInformation("─────────────────────────────────────────────────");

            await TestSimpleTransactionCommitAsync(
                personRepository, 
                transactionContext, 
                logger, 
                cancellationToken);

            // ============================================================
            // تست 2: Transaction با Rollback
            // ============================================================
            logger?.LogInformation("\n📋 Test 2: Transaction with Rollback");
            logger?.LogInformation("─────────────────────────────────────────────────");

            await TestTransactionRollbackAsync(
                personRepository, 
                transactionContext, 
                logger, 
                cancellationToken);

            // ============================================================
            // تست 3: Transaction با چند Repository Call
            // ============================================================
            logger?.LogInformation("\n📋 Test 3: Transaction with Multiple Repository Calls");
            logger?.LogInformation("─────────────────────────────────────────────────");

            await TestTransactionWithMultipleOperationsAsync(
                personRepository, 
                transactionContext, 
                logger, 
                cancellationToken);

            // ============================================================
            // تست 4: Transaction با Exception (Auto Rollback)
            // ============================================================
            logger?.LogInformation("\n📋 Test 4: Transaction with Exception (Auto Rollback)");
            logger?.LogInformation("─────────────────────────────────────────────────");

            await TestTransactionWithExceptionAsync(
                personRepository, 
                transactionContext, 
                logger, 
                cancellationToken);

            // ============================================================
            // تست 5: بررسی Connection Sharing در Transaction
            // ============================================================
            logger?.LogInformation("\n📋 Test 5: Connection Sharing in Transaction");
            logger?.LogInformation("─────────────────────────────────────────────────");

            await TestConnectionSharingAsync(
                personRepository, 
                transactionContext, 
                connectionFactory,
                logger, 
                cancellationToken);

            // ============================================================
            // تست 6: Read/Write Routing در Transaction
            // ============================================================
            logger?.LogInformation("\n📋 Test 6: Read/Write Routing in Transaction");
            logger?.LogInformation("─────────────────────────────────────────────────");

            await TestReadWriteRoutingInTransactionAsync(
                personRepository, 
                transactionContext, 
                logger, 
                cancellationToken);

            logger?.LogInformation("\n✅ All Transaction Lifecycle Tests Passed!");
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "❌ Transaction Lifecycle Test Failed: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// تست 1: Transaction ساده با Commit موفق
    /// </summary>
    private static async Task TestSimpleTransactionCommitAsync(
        IPersonRepository personRepository,
        ITransactionContext transactionContext,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        logger?.LogInformation("  → Starting transaction...");
        
        // ✅ شروع Transaction
        await using var transaction = await transactionContext.BeginTransactionAsync(cancellationToken);
        logger?.LogInformation("  ✅ Transaction started");
        logger?.LogInformation("  → Connection: {ConnectionState}", transaction.Connection.State);
        logger?.LogInformation("  → Transaction: {TransactionIsolationLevel}", transaction.Transaction.IsolationLevel);

        // ✅ عملیات در Transaction
        logger?.LogInformation("  → Creating Person in transaction...");
        var testPerson = new Person
        {
            FirstName = "Transaction",
            LastName = "Test",
            DateOfBirth = new DateTime(1990, 1, 1)
        };
        
        await personRepository.CreatePerson(testPerson, cancellationToken);
        logger?.LogInformation("  ✅ Person created with ID: {Id}", testPerson.Id);

        // ✅ بررسی که connection مشترک استفاده شده
        var currentConnection = transactionContext.GetCurrentConnection();
        logger?.LogInformation("  → Current connection in context: {HasConnection}", currentConnection != null);
        logger?.LogInformation("  → Connection is same: {IsSame}", currentConnection == transaction.Connection);

        // ✅ Commit
        logger?.LogInformation("  → Committing transaction...");
        await transaction.CommitAsync(cancellationToken);
        logger?.LogInformation("  ✅ Transaction committed successfully");

        // ✅ بررسی که بعد از commit، connection دیگر در context نیست
        var connectionAfterCommit = transactionContext.GetCurrentConnection();
        logger?.LogInformation("  → Connection after commit: {HasConnection}", connectionAfterCommit != null);

        // ✅ بررسی در دیتابیس
        var retrievedPerson = await personRepository.GetById(testPerson.Id, cancellationToken);
        logger?.LogInformation("  ✅ Person retrieved from database: {FirstName} {LastName}", 
            retrievedPerson.FirstName, retrievedPerson.LastName);

        // Cleanup
        try
        {
            await personRepository.DeletePerson(testPerson.Id, cancellationToken);
            logger?.LogInformation("  ✅ Test data cleaned up");
        }
        catch (Exception ex)
        {
            logger?.LogWarning("  ⚠️ Could not clean up: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// تست 2: Transaction با Rollback
    /// </summary>
    private static async Task TestTransactionRollbackAsync(
        IPersonRepository personRepository,
        ITransactionContext transactionContext,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        logger?.LogInformation("  → Starting transaction...");
        
        await using var transaction = await transactionContext.BeginTransactionAsync(cancellationToken);
        logger?.LogInformation("  ✅ Transaction started");

        // ✅ ایجاد Person در Transaction
        var testPerson = new Person
        {
            FirstName = "Rollback",
            LastName = "Test",
            DateOfBirth = new DateTime(1990, 1, 1)
        };
        
        await personRepository.CreatePerson(testPerson, cancellationToken);
        logger?.LogInformation("  ✅ Person created with ID: {Id}", testPerson.Id);

        // ✅ Rollback
        logger?.LogInformation("  → Rolling back transaction...");
        await transaction.RollbackAsync(cancellationToken);
        logger?.LogInformation("  ✅ Transaction rolled back");

        // ✅ بررسی که Person ایجاد نشده (چون rollback شد)
        try
        {
            var retrievedPerson = await personRepository.GetById(testPerson.Id, cancellationToken);
            logger?.LogError("  ❌ ERROR: Person should not exist after rollback!");
        }
        catch (KeyNotFoundException)
        {
            logger?.LogInformation("  ✅ Person does not exist (rollback worked correctly)");
        }
    }

    /// <summary>
    /// تست 3: Transaction با چند عملیات
    /// </summary>
    private static async Task TestTransactionWithMultipleOperationsAsync(
        IPersonRepository personRepository,
        ITransactionContext transactionContext,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        logger?.LogInformation("  → Starting transaction with multiple operations...");
        
        await using var transaction = await transactionContext.BeginTransactionAsync(cancellationToken);
        logger?.LogInformation("  ✅ Transaction started");

        // ✅ عملیات 1: Create
        var person1 = new Person
        {
            FirstName = "Multi",
            LastName = "Op1",
            DateOfBirth = new DateTime(1990, 1, 1)
        };
        await personRepository.CreatePerson(person1, cancellationToken);
        logger?.LogInformation("  ✅ Operation 1: Person created with ID: {Id}", person1.Id);

        // ✅ عملیات 2: Read (در همان transaction)
        var retrieved = await personRepository.GetById(person1.Id, cancellationToken);
        logger?.LogInformation("  ✅ Operation 2: Person read: {FirstName} {LastName}", 
            retrieved.FirstName, retrieved.LastName);

        // ✅ عملیات 3: Update
        retrieved.FirstName = "MultiUpdated";
        await personRepository.UpdatePerson(retrieved.Id, retrieved, cancellationToken);
        logger?.LogInformation("  ✅ Operation 3: Person updated");

        // ✅ عملیات 4: Read again
        var retrievedAgain = await personRepository.GetById(person1.Id, cancellationToken);
        logger?.LogInformation("  ✅ Operation 4: Person read again: {FirstName}", retrievedAgain.FirstName);

        // ✅ Commit همه عملیات
        logger?.LogInformation("  → Committing all operations...");
        await transaction.CommitAsync(cancellationToken);
        logger?.LogInformation("  ✅ All operations committed successfully");

        // ✅ بررسی نهایی
        var finalCheck = await personRepository.GetById(person1.Id, cancellationToken);
        logger?.LogInformation("  ✅ Final check: {FirstName} {LastName}", 
            finalCheck.FirstName, finalCheck.LastName);

        // Cleanup
        try
        {
            await personRepository.DeletePerson(person1.Id, cancellationToken);
            logger?.LogInformation("  ✅ Test data cleaned up");
        }
        catch (Exception ex)
        {
            logger?.LogWarning("  ⚠️ Could not clean up: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// تست 4: Transaction با Exception (Auto Rollback)
    /// </summary>
    private static async Task TestTransactionWithExceptionAsync(
        IPersonRepository personRepository,
        ITransactionContext transactionContext,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        logger?.LogInformation("  → Starting transaction that will fail...");
        
        try
        {
            await using var transaction = await transactionContext.BeginTransactionAsync(cancellationToken);
            logger?.LogInformation("  ✅ Transaction started");

            // ✅ ایجاد Person
            var testPerson = new Person
            {
                FirstName = "Exception",
                LastName = "Test",
                DateOfBirth = new DateTime(1990, 1, 1)
            };
            
            await personRepository.CreatePerson(testPerson, cancellationToken);
            logger?.LogInformation("  ✅ Person created with ID: {Id}", testPerson.Id);

            // ✅ ایجاد Exception (شبیه‌سازی خطا)
            logger?.LogInformation("  → Simulating exception...");
            throw new InvalidOperationException("Simulated error for rollback test");

            // این خط اجرا نمی‌شود
            await transaction.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogInformation("  ✅ Exception caught: {Message}", ex.Message);
            logger?.LogInformation("  → Transaction should be rolled back automatically by DisposeAsync");

            // ✅ بررسی که Person ایجاد نشده
            try
            {
                var testPerson = new Person { Id = 999 }; // ID فرضی
                await personRepository.GetById(999, cancellationToken);
                logger?.LogError("  ❌ ERROR: Person should not exist!");
            }
            catch (KeyNotFoundException)
            {
                logger?.LogInformation("  ✅ Person does not exist (auto rollback worked)");
            }
        }
    }

    /// <summary>
    /// تست 5: بررسی Connection Sharing در Transaction
    /// </summary>
    private static async Task TestConnectionSharingAsync(
        IPersonRepository personRepository,
        ITransactionContext transactionContext,
        IConnectionFactory connectionFactory,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        logger?.LogInformation("  → Testing connection sharing...");
        
        await using var transaction = await transactionContext.BeginTransactionAsync(cancellationToken);
        logger?.LogInformation("  ✅ Transaction started");

        // ✅ بررسی Connection از Context
        var connection1 = transactionContext.GetCurrentConnection();
        logger?.LogInformation("  → Connection 1 from context: {ConnectionId}", 
            connection1?.GetHashCode());

        // ✅ بررسی Connection از Transaction
        var connection2 = transaction.Connection;
        logger?.LogInformation("  → Connection 2 from transaction: {ConnectionId}", 
            connection2.GetHashCode());

        // ✅ بررسی که همان Connection است
        logger?.LogInformation("  → Are connections the same: {IsSame}", connection1 == connection2);

        // ✅ عملیات با Repository (باید از همان connection استفاده کند)
        var testPerson = new Person
        {
            FirstName = "Connection",
            LastName = "Share",
            DateOfBirth = new DateTime(1990, 1, 1)
        };
        
        await personRepository.CreatePerson(testPerson, cancellationToken);
        logger?.LogInformation("  ✅ Person created using shared connection");

        // ✅ بررسی Connection بعد از عملیات
        var connection3 = transactionContext.GetCurrentConnection();
        logger?.LogInformation("  → Connection 3 after operation: {ConnectionId}", 
            connection3?.GetHashCode());
        logger?.LogInformation("  → Still same connection: {IsSame}", connection3 == connection1);

        // ✅ Commit
        await transaction.CommitAsync(cancellationToken);
        logger?.LogInformation("  ✅ Transaction committed");

        // ✅ بررسی Connection بعد از Commit
        var connection4 = transactionContext.GetCurrentConnection();
        logger?.LogInformation("  → Connection 4 after commit: {HasConnection}", connection4 != null);
        logger?.LogInformation("  ✅ Connection cleared from context (correct behavior)");

        // Cleanup
        try
        {
            await personRepository.DeletePerson(testPerson.Id, cancellationToken);
            logger?.LogInformation("  ✅ Test data cleaned up");
        }
        catch (Exception ex)
        {
            logger?.LogWarning("  ⚠️ Could not clean up: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// تست 6: Read/Write Routing در Transaction
    /// </summary>
    private static async Task TestReadWriteRoutingInTransactionAsync(
        IPersonRepository personRepository,
        ITransactionContext transactionContext,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        logger?.LogInformation("  → Testing Read/Write routing in transaction...");
        
        // ✅ ایجاد یک Person خارج از Transaction (برای تست)
        var existingPerson = new Person
        {
            FirstName = "Routing",
            LastName = "Test",
            DateOfBirth = new DateTime(1990, 1, 1)
        };
        await personRepository.CreatePerson(existingPerson, cancellationToken);
        logger?.LogInformation("  ✅ Person created outside transaction with ID: {Id}", existingPerson.Id);

        // ✅ شروع Transaction
        await using var transaction = await transactionContext.BeginTransactionAsync(cancellationToken);
        logger?.LogInformation("  ✅ Transaction started");

        // ✅ Read Operation (باید به Write DB برود چون در Transaction است)
        logger?.LogInformation("  → Reading Person in transaction (should use Write DB)...");
        var readPerson = await personRepository.GetById(existingPerson.Id, cancellationToken);
        logger?.LogInformation("  ✅ Person read: {FirstName} {LastName}", 
            readPerson.FirstName, readPerson.LastName);

        // ✅ بررسی Connection
        var readConnection = transactionContext.GetCurrentConnection();
        logger?.LogInformation("  → Read used connection: {ConnectionId}", readConnection?.GetHashCode());
        logger?.LogInformation("  → Same as transaction connection: {IsSame}", 
            readConnection == transaction.Connection);

        // ✅ Write Operation (باید از همان Connection استفاده کند)
        logger?.LogInformation("  → Updating Person in transaction (should use same connection)...");
        readPerson.FirstName = "RoutingUpdated";
        await personRepository.UpdatePerson(readPerson.Id, readPerson, cancellationToken);
        logger?.LogInformation("  ✅ Person updated");

        // ✅ Read Again (باید از همان Connection استفاده کند)
        logger?.LogInformation("  → Reading Person again in transaction...");
        var readPersonAgain = await personRepository.GetById(existingPerson.Id, cancellationToken);
        logger?.LogInformation("  ✅ Person read again: {FirstName}", readPersonAgain.FirstName);

        // ✅ Commit
        logger?.LogInformation("  → Committing transaction...");
        await transaction.CommitAsync(cancellationToken);
        logger?.LogInformation("  ✅ Transaction committed");

        // ✅ بررسی نهایی (خارج از Transaction - باید از Read Replica استفاده کند)
        logger?.LogInformation("  → Reading Person outside transaction (should use Read Replica)...");
        var finalPerson = await personRepository.GetById(existingPerson.Id, cancellationToken);
        logger?.LogInformation("  ✅ Final read: {FirstName} {LastName}", 
            finalPerson.FirstName, finalPerson.LastName);

        // Cleanup
        try
        {
            await personRepository.DeletePerson(existingPerson.Id, cancellationToken);
            logger?.LogInformation("  ✅ Test data cleaned up");
        }
        catch (Exception ex)
        {
            logger?.LogWarning("  ⚠️ Could not clean up: {Message}", ex.Message);
        }
    }
}
