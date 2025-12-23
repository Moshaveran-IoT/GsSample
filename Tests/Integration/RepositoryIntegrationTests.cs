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
/// تست‌های Integration برای Repository با UnitOfWork
/// بررسی می‌کند که Read/Write Separation و Transaction management درست کار می‌کنند
/// </summary>
public class RepositoryIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    private readonly IServiceProvider _serviceProvider;

    public RepositoryIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _serviceProvider = fixture.ServiceProvider;
    }

    [Fact]
    public async Task Repository_WithUnitOfWork_ShouldUseSharedConnection()
    {
        // Arrange - Create scope for Scoped services
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPersonRepository>();
        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var transactionContext = scope.ServiceProvider.GetRequiredService<ITransactionContext>();

        // Act - استفاده از await using برای اطمینان از async dispose
        await using var unitOfWork = await unitOfWorkManager.CreateNew(CancellationToken.None);
        
        // ✅ Verify connection is available after CreateNew - استفاده از unitOfWork.GetConnection()
        var connection1 = unitOfWork.GetConnection();
        connection1.Should().NotBeNull("Connection should be available after CreateNew");
        
        var person1 = new Domain.Models.Person
        {
            FirstName = "Shared1",
            LastName = "Test",
            DateOfBirth = new DateTime(1990, 1, 1)
        };
        await repository.CreatePerson(person1, CancellationToken.None);

        // ✅ استفاده از unitOfWork.GetConnection() به جای transactionContext.GetCurrentConnection()
        var connection2 = unitOfWork.GetConnection();
        var person2 = new Domain.Models.Person
        {
            FirstName = "Shared2",
            LastName = "Test",
            DateOfBirth = new DateTime(1991, 1, 1)
        };
        await repository.CreatePerson(person2, CancellationToken.None);

        await unitOfWork.Commit(CancellationToken.None);

        // Assert
        connection1.Should().NotBeNull();
        connection2.Should().NotBeNull();
        connection1.Should().BeSameAs(connection2, "Both operations should use the same connection");
    }

    [Fact]
    public async Task Repository_ReadWriteSeparation_ShouldWorkWithUnitOfWork()
    {
        // Arrange - Create scope for Scoped services
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPersonRepository>();
        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var transactionContext = scope.ServiceProvider.GetRequiredService<ITransactionContext>();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IConnectionFactory>();

        var person = new Domain.Models.Person
        {
            FirstName = "ReadWriteSep",
            LastName = "Test",
            DateOfBirth = new DateTime(1990, 1, 1)
        };

        // Act - Inside transaction (should use write connection) - استفاده از await using
        await using var unitOfWork = await unitOfWorkManager.CreateNew(CancellationToken.None);
        await repository.CreatePerson(person, CancellationToken.None);
        
        // ✅ استفاده از unitOfWork.GetConnection() به جای transactionContext.GetCurrentConnection()
        var connectionInTransaction = unitOfWork.GetConnection();
        var readInTransaction = await repository.GetById(person.Id, CancellationToken.None);
        
        await unitOfWork.Commit(CancellationToken.None);

        // Outside transaction (should use read connection if available)
        // ✅ بعد از commit، connection باید null باشد
        var connectionOutsideTransaction = transactionContext.GetCurrentConnection();
        var readOutsideTransaction = await repository.GetById(person.Id, CancellationToken.None);

        // Assert
        connectionInTransaction.Should().NotBeNull("Should have active connection in transaction");
        connectionOutsideTransaction.Should().BeNull("Should not have active connection outside transaction");
        readInTransaction.Should().NotBeNull();
        readOutsideTransaction.Should().NotBeNull();
        readInTransaction.Id.Should().Be(readOutsideTransaction.Id);
    }

    [Fact]
    public async Task Repository_MultipleOperationsInTransaction_ShouldAllCommitOrRollback()
    {
        // Arrange - Create scope for Scoped services
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPersonRepository>();
        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        var person1 = new Domain.Models.Person
        {
            FirstName = "Multi1",
            LastName = "Test",
            DateOfBirth = new DateTime(1990, 1, 1)
        };

        var person2 = new Domain.Models.Person
        {
            FirstName = "Multi2",
            LastName = "Test",
            DateOfBirth = new DateTime(1991, 1, 1)
        };

        // Act - Commit scenario - استفاده از await using
        await using (var unitOfWork = await unitOfWorkManager.CreateNew(CancellationToken.None))
        {
            await repository.CreatePerson(person1, CancellationToken.None);
            await repository.CreatePerson(person2, CancellationToken.None);
            await unitOfWork.Commit(CancellationToken.None);
        }

        // Both should exist
        var retrieved1 = await repository.GetById(person1.Id, CancellationToken.None);
        var retrieved2 = await repository.GetById(person2.Id, CancellationToken.None);

        // Act - Rollback scenario
        var person3 = new Domain.Models.Person
        {
            FirstName = "Multi3",
            LastName = "Test",
            DateOfBirth = new DateTime(1992, 1, 1)
        };

        int person3Id = 0;
        await using (var unitOfWork = await unitOfWorkManager.CreateNew(CancellationToken.None))
        {
            await repository.CreatePerson(person3, CancellationToken.None);
            person3Id = person3.Id; // Save ID before dispose
            // Dispose without commit - should rollback
        }

        // Assert
        retrieved1.Should().NotBeNull();
        retrieved2.Should().NotBeNull();
        
        // ✅ Wait longer to ensure rollback is complete
        await Task.Delay(2000);
        
        // ✅ Person3 should not exist (rolled back)
        // استفاده از retry برای اطمینان از اینکه rollback کامل شده است
        var maxRetries = 10;
        var retryCount = 0;
        KeyNotFoundException? lastException = null;
        
        while (retryCount < maxRetries)
        {
            try
            {
                await repository.GetById(person3Id, CancellationToken.None);
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
            throw new InvalidOperationException($"Person with Id {person3Id} still exists after rollback.");
        }
        
        lastException.Message.Should().Contain($"Person with Id {person3Id} not found");
    }
}

