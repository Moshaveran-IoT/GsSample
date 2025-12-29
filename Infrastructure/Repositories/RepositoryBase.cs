using System.Data.Common;

using Infrastructure.Extensions;
using Infrastructure.Interfaces;

namespace Infrastructure.Repositories;

public class RepositoryBase(IConnectionFactory connectionFactory, ITransactionContext transactionContext)
{
    protected readonly IConnectionFactory _connectionFactory = connectionFactory ?? throw new ArgumentNullException();
    protected readonly ITransactionContext _transactionContext = transactionContext ?? throw new ArgumentNullException();

    protected async Task Write(Func<DbConnection, CancellationToken, Task> command, CancellationToken cancellationToken) => await ConnectionExtensions.ExecuteWriteCommandAsync(
                this._connectionFactory,
                this._transactionContext,
                command,
                cancellationToken);
}