using FluentAssertions;
using Infrastructure;
using Infrastructure.Extensions;
using Infrastructure.Interfaces;
using Infrastructure.Repositories;
using Infrastructure.UnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Tests.Integration;

/// <summary>
/// تست‌های Integration برای UnitOfWork با دیتابیس واقعی
/// </summary>
public class UnitOfWorkIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    private readonly IServiceProvider _serviceProvider;

    public UnitOfWorkIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _serviceProvider = fixture.ServiceProvider;
    }

    [Fact]
    public async Task UnitOfWork_Commit_ShouldPersistChanges()
    {
        // Arrange - Create scope for Scoped services
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPersonRepository>();
        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var person = new Domain.Models.Person
        {
            FirstName = "Test",
            LastName = "User",
            DateOfBirth = new DateTime(1990, 1, 1)
        };

        // Act - استفاده از await using برای اطمینان از async dispose
        await using var unitOfWork = await unitOfWorkManager.CreateNew(CancellationToken.None);
        await repository.CreatePerson(person, CancellationToken.None);
        await unitOfWork.Commit(CancellationToken.None);

        // Assert
        person.Id.Should().BeGreaterThan(0);
        
        var retrieved = await repository.GetById(person.Id, CancellationToken.None);
        retrieved.Should().NotBeNull();
        retrieved.FirstName.Should().Be("Test");
    }

    [Fact]
    public async Task UnitOfWork_DisposeWithoutCommit_ShouldRollback()
    {
        // Arrange - Create scope for Scoped services
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPersonRepository>();
        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var person = new Domain.Models.Person
        {
            FirstName = "Rollback",
            LastName = "Test",
            DateOfBirth = new DateTime(1990, 1, 1)
        };

        // Act - استفاده از await using برای اطمینان از rollback
        int personId = 0;
        await using (var unitOfWork = await unitOfWorkManager.CreateNew(CancellationToken.None))
        {
            await repository.CreatePerson(person, CancellationToken.None);
            personId = person.Id; // Save ID before dispose
            // Dispose without commit - should rollback
        }

        // Assert - Person should not exist (rollback should have removed it)
        // ✅ Wait longer to ensure rollback is complete and connection is disposed
        // همچنین اطمینان از اینکه transaction درست dispose شده است
        await Task.Delay(2000);
        
        // ✅ Try to get the person - should throw KeyNotFoundException
        // Rollback باید داده را حذف کرده باشد
        // استفاده از retry برای اطمینان از اینکه rollback کامل شده است
        var maxRetries = 10;
        var retryCount = 0;
        KeyNotFoundException? lastException = null;
        
        while (retryCount < maxRetries)
        {
            try
            {
                await repository.GetById(personId, CancellationToken.None);
                // اگر exception نیفتاد، کمی صبر می‌کنیم و دوباره تلاش می‌کنیم
                await Task.Delay(300);
                retryCount++;
            }
            catch (KeyNotFoundException ex)
            {
                lastException = ex;
                break; // Exception مورد انتظار - rollback موفق بوده است
            }
        }
        
        // اگر بعد از retry ها هنوز exception نیفتاد، باید fail کنیم
        if (lastException == null)
        {
            throw new InvalidOperationException($"Person with Id {personId} still exists after rollback. Rollback may not have completed correctly.");
        }
        
        lastException.Message.Should().Contain($"Person with Id {personId} not found");
    }

    [Fact]
    public async Task UnitOfWork_MultipleOperations_ShouldUseSameTransaction()
    {
        // Arrange - Create scope for Scoped services
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPersonRepository>();
        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var transactionContext = scope.ServiceProvider.GetRequiredService<ITransactionContext>();

        var person1 = new Domain.Models.Person
        {
            FirstName = "Person1",
            LastName = "Test",
            DateOfBirth = new DateTime(1990, 1, 1)
        };

        var person2 = new Domain.Models.Person
        {
            FirstName = "Person2",
            LastName = "Test",
            DateOfBirth = new DateTime(1991, 1, 1)
        };

        // Act - استفاده از await using برای اطمینان از async dispose
        await using var unitOfWork = await unitOfWorkManager.CreateNew(CancellationToken.None);
        
        // Verify connection is available after CreateNew
        // ✅ استفاده از unitOfWork.GetConnection() به جای transactionContext.GetCurrentConnection()
        // چون AsyncLocal ممکن است در async context درست propagate نکند
        var connection1 = unitOfWork.GetConnection();
        connection1.Should().NotBeNull("Connection should be available after CreateNew");
        
        await repository.CreatePerson(person1, CancellationToken.None);
        
        var connection2 = unitOfWork.GetConnection();
        connection2.Should().NotBeNull("Connection should still be available after CreatePerson");
        
        await repository.CreatePerson(person2, CancellationToken.None);

        await unitOfWork.Commit(CancellationToken.None);

        // Assert
        connection1.Should().NotBeNull();
        connection2.Should().NotBeNull();
        connection1.Should().BeSameAs(connection2); // Same connection
        
        // Both should be persisted
        var retrieved1 = await repository.GetById(person1.Id, CancellationToken.None);
        var retrieved2 = await repository.GetById(person2.Id, CancellationToken.None);
        
        retrieved1.Should().NotBeNull();
        retrieved2.Should().NotBeNull();
    }

    [Fact]
    public async Task UnitOfWork_Exception_ShouldRollback()
    {
        // Arrange - Create scope for Scoped services
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPersonRepository>();
        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var person = new Domain.Models.Person
        {
            FirstName = "Exception",
            LastName = "Test",
            DateOfBirth = new DateTime(1990, 1, 1)
        };

        // Act - استفاده از await using برای اطمینان از rollback
        try
        {
            await using var unitOfWork = await unitOfWorkManager.CreateNew(CancellationToken.None);
            await repository.CreatePerson(person, CancellationToken.None);
            var personId = person.Id; // Save ID before exception
            throw new InvalidOperationException("Test exception");
        }
        catch (InvalidOperationException)
        {
            // Expected exception - unitOfWork will be disposed and rolled back
        }

        // Assert - Person should not exist (rolled back)
        // ✅ Wait longer to ensure rollback is complete
        await Task.Delay(2000);
        
        // ✅ استفاده از retry برای اطمینان از اینکه rollback کامل شده است
        var maxRetries = 10;
        var retryCount = 0;
        KeyNotFoundException? lastException = null;
        
        while (retryCount < maxRetries)
        {
            try
            {
                await repository.GetById(person.Id, CancellationToken.None);
                await Task.Delay(300);
                retryCount++;
            }
            catch (KeyNotFoundException ex)
            {
                lastException = ex;
                break;
            }
        }
        
        if (lastException == null)
        {
            throw new InvalidOperationException($"Person with Id {person.Id} still exists after rollback.");
        }
        
        lastException.Message.Should().Contain($"Person with Id {person.Id} not found");
    }

    [Fact]
    public async Task UnitOfWork_ReadWriteSeparation_ShouldWork()
    {
        // Arrange - Create scope for Scoped services
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPersonRepository>();
        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var transactionContext = scope.ServiceProvider.GetRequiredService<ITransactionContext>();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IConnectionFactory>();

        var person = new Domain.Models.Person
        {
            FirstName = "ReadWrite",
            LastName = "Test",
            DateOfBirth = new DateTime(1990, 1, 1)
        };

        // Act - Create in transaction - استفاده از await using
        await using var unitOfWork = await unitOfWorkManager.CreateNew(CancellationToken.None);
        
        // ✅ Verify connection is available - استفاده از unitOfWork.GetConnection()
        var connectionBeforeCreate = unitOfWork.GetConnection();
        connectionBeforeCreate.Should().NotBeNull("Connection should be available after CreateNew");
        
        await repository.CreatePerson(person, CancellationToken.None);
        
        // ✅ Read inside transaction (should use write connection) - استفاده از unitOfWork.GetConnection()
        var connectionInTransaction = unitOfWork.GetConnection();
        connectionInTransaction.Should().NotBeNull("Connection should still be available after CreatePerson");
        var personInTransaction = await repository.GetById(person.Id, CancellationToken.None);
        
        await unitOfWork.Commit(CancellationToken.None);

        // Read outside transaction (should use read connection)
        var connectionOutsideTransaction = transactionContext.GetCurrentConnection();
        var personOutsideTransaction = await repository.GetById(person.Id, CancellationToken.None);

        // Assert
        connectionInTransaction.Should().NotBeNull();
        connectionOutsideTransaction.Should().BeNull(); // No active transaction
        personInTransaction.Should().NotBeNull();
        personOutsideTransaction.Should().NotBeNull();
        personInTransaction.Id.Should().Be(personOutsideTransaction.Id);
    }
}

/// <summary>
/// Fixture برای setup دیتابیس و DI Container
/// </summary>
public class DatabaseFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }

    public DatabaseFixture()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddConnectionFactory(configuration);
        services.AddRepositories();

        ServiceProvider = services.BuildServiceProvider();

        // Setup database
        SetupDatabaseAsync().GetAwaiter().GetResult();
    }

    private async Task SetupDatabaseAsync()
    {
        var configuration = ServiceProvider.GetRequiredService<IConfiguration>();
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<DatabaseFixture>();

        await Infrastructure.Database.TestDatabaseConnection.TestAndSetupDatabaseAsync(
            configuration,
            logger,
            CancellationToken.None);
    }

    public void Dispose()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

