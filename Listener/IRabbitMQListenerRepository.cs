
using Domain.Models;

namespace Listener;

public interface IRabbitMQListenerRepository
{
    Task CreatePerson(Person person, CancellationToken cancellationToken);
}