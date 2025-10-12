
using Domain.Models;

namespace Listener;

internal class RabbitMQListenerRepository : IRabbitMQListenerRepository
{
    public Task CreatePerson(Person person, CancellationToken cancellationToken) => Task.CompletedTask;
}