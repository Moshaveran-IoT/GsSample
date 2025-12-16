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
        // ✅ استفاده از Helper برای Write Command
        // این Helper به صورت خودکار:
        // - اگر transaction فعال باشد: از connection مشترک استفاده می‌کند (و dispose نمی‌کند)
        // - در غیر این صورت: connection جدید از Write DB می‌سازد (و dispose می‌کند)
        const string sql = @"
            INSERT INTO Persons (FirstName, LastName, DateOfBirth)
            OUTPUT INSERTED.Id
            VALUES (@FirstName, @LastName, @DateOfBirth)";
        
        person.Id = await ConnectionExtensions.ExecuteWriteCommandAsync(
            _connectionFactory,
            _transactionContext,
            async (db, ct) => await db.QuerySingleAsync<int>(sql, person, cancellationToken: ct),
            cancellationToken);
    }
}
