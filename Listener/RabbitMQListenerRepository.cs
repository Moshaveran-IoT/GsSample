using System.Data;
using System.Data.Common;
using Dapper;
using Domain.Models;
using Infrastructure.Extensions;
using Infrastructure.Interfaces;

namespace Listener;

/// <summary>
/// پیاده‌سازی IRabbitMQListenerRepository با استفاده از ConnectionFactory و TransactionContext.
/// 
/// این Repository از Write Connection استفاده می‌کند (چون Command است).
/// </summary>
internal class RabbitMQListenerRepository : IRabbitMQListenerRepository
{
    private readonly Infrastructure.Interfaces.IConnectionFactory _connectionFactory;
    private readonly ITransactionContext _transactionContext;

    public RabbitMQListenerRepository(
        Infrastructure.Interfaces.IConnectionFactory connectionFactory,
        ITransactionContext transactionContext)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _transactionContext = transactionContext ?? throw new ArgumentNullException(nameof(transactionContext));
    }

    public async Task CreatePerson(Person person, CancellationToken cancellationToken)
    {
        // ✅ استفاده مستقیم از connection - ساده و واضح
        // اگر transaction فعال باشد، از connection مشترک استفاده می‌کند
        // در غیر این صورت، connection جدید می‌سازد و dispose می‌شود
        var currentConnection = _transactionContext.GetCurrentConnection();
        
        if (currentConnection != null)
        {
            // ✅ استفاده از connection مشترک (transaction فعال است)
            // ✅ استفاده از OUTPUT INSERTED.Id برای اطمینان از rollback صحیح
            const string sql = @"
                INSERT INTO Persons (FirstName, LastName, DateOfBirth)
                OUTPUT INSERTED.Id
                VALUES (@FirstName, @LastName, @DateOfBirth)";

            var command = new CommandDefinition(sql, person, cancellationToken: cancellationToken);
            person.Id = await currentConnection.QuerySingleAsync<int>(command);
        }
        else
        {
            // ✅ ایجاد connection جدید و dispose خودکار
            await using var db = await _connectionFactory.CreateWriteConnection(cancellationToken);
            
            // ✅ استفاده از OUTPUT INSERTED.Id برای اطمینان از rollback صحیح
            const string sql = @"
                INSERT INTO Persons (FirstName, LastName, DateOfBirth)
                OUTPUT INSERTED.Id
                VALUES (@FirstName, @LastName, @DateOfBirth)";

            var command = new CommandDefinition(sql, person, cancellationToken: cancellationToken);
            person.Id = await db.QuerySingleAsync<int>(command);
        }
    }
}
