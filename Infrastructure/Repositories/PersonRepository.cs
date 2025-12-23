using System.Data;
using System.Data.Common;
using Dapper;
using Domain.Models;
using Infrastructure.Extensions;
using Infrastructure.Interfaces;

namespace Infrastructure.Repositories;

/// <summary>
/// پیاده‌سازی IPersonRepository با استفاده از ConnectionFactory و TransactionContext.
/// 
/// این Repository از Read/Write Separation استفاده می‌کند:
/// - Read operations: از Read Replica استفاده می‌کند (مگر اینکه transaction فعال باشد)
/// - Write operations: از Write DB استفاده می‌کند
/// </summary>
public sealed class PersonRepository : IPersonRepository
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ITransactionContext _transactionContext;

    public PersonRepository(
        IConnectionFactory connectionFactory,
        ITransactionContext transactionContext)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _transactionContext = transactionContext ?? throw new ArgumentNullException(nameof(transactionContext));
    }

    /// <summary>
    /// دریافت Person با ID مشخص.
    /// از Read Connection استفاده می‌کند (مگر اینکه transaction فعال باشد).
    /// </summary>
    public async Task<Person> GetById(int id, CancellationToken cancellationToken)
    {
        return await ConnectionExtensions.ExecuteReadQueryAsync(
            _connectionFactory,
            _transactionContext,
            async (db, ct) =>
            {
                const string sql = @"
                    SELECT Id, FirstName, LastName, DateOfBirth 
                    FROM Persons 
                    WHERE Id = @Id";
                
                var command = new CommandDefinition(
                    sql,
                    new { Id = id },
                    cancellationToken: ct);
                
                var person = await db.QueryFirstOrDefaultAsync<Person>(command);
                
                return person ?? throw new KeyNotFoundException($"Person with Id {id} not found");
            },
            cancellationToken);
    }

    /// <summary>
    /// دریافت همه Persons.
    /// از Read Connection استفاده می‌کند (مگر اینکه transaction فعال باشد).
    /// </summary>
    public async Task<IEnumerable<Person>> GetAll(CancellationToken cancellationToken)
    {
        return await ConnectionExtensions.ExecuteReadQueryAsync(
            _connectionFactory,
            _transactionContext,
            async (db, ct) =>
            {
                const string sql = @"
                    SELECT Id, FirstName, LastName, DateOfBirth 
                    FROM Persons 
                    ORDER BY Id";
                
                var command = new CommandDefinition(sql, cancellationToken: ct);
                return await db.QueryAsync<Person>(command);
            },
            cancellationToken);
    }

    /// <summary>
    /// ایجاد Person جدید.
    /// از Write Connection استفاده می‌کند.
    /// </summary>
    public async Task CreatePerson(Person person, CancellationToken cancellationToken)
    {
        person.Id = await ConnectionExtensions.ExecuteWriteCommandAsync(
            _connectionFactory,
            _transactionContext,
            async (db, ct) =>
            {
                // ✅ استفاده از OUTPUT INSERTED.Id برای اطمینان از rollback صحیح
                // OUTPUT INSERTED.Id ID را در همان transaction برمی‌گرداند
                // اگر rollback انجام شود، داده و ID هر دو حذف می‌شوند
                const string sql = @"
                    INSERT INTO Persons (FirstName, LastName, DateOfBirth)
                    OUTPUT INSERTED.Id
                    VALUES (@FirstName, @LastName, @DateOfBirth)";
                
                var command = new CommandDefinition(sql, person, cancellationToken: ct);
                return await db.QuerySingleAsync<int>(command);
            },
            cancellationToken);
    }

    /// <summary>
    /// به‌روزرسانی Person موجود.
    /// از Write Connection استفاده می‌کند.
    /// </summary>
    public async Task UpdatePerson(int id, Person person, CancellationToken cancellationToken)
    {
        var rowsAffected = await ConnectionExtensions.ExecuteWriteCommandAsync(
            _connectionFactory,
            _transactionContext,
            async (db, ct) =>
            {
                const string sql = @"
                    UPDATE Persons 
                    SET FirstName = @FirstName, 
                        LastName = @LastName, 
                        DateOfBirth = @DateOfBirth
                    WHERE Id = @Id";
                
                var command = new CommandDefinition(
                    sql,
                    new { Id = id, person.FirstName, person.LastName, person.DateOfBirth },
                    cancellationToken: ct);
                
                return await db.ExecuteAsync(command);
            },
            cancellationToken);

        if (rowsAffected == 0)
        {
            throw new KeyNotFoundException($"Person with Id {id} not found");
        }
    }

    /// <summary>
    /// حذف Person.
    /// از Write Connection استفاده می‌کند.
    /// </summary>
    public async Task DeletePerson(int id, CancellationToken cancellationToken)
    {
        var rowsAffected = await ConnectionExtensions.ExecuteWriteCommandAsync(
            _connectionFactory,
            _transactionContext,
            async (db, ct) =>
            {
                const string sql = "DELETE FROM Persons WHERE Id = @Id";
                var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: ct);
                return await db.ExecuteAsync(command);
            },
            cancellationToken);

        if (rowsAffected == 0)
        {
            throw new KeyNotFoundException($"Person with Id {id} not found");
        }
    }
}

