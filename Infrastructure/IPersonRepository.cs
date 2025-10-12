using Domain.Models;

namespace Infrastructure;

public interface IPersonRepository
{
    Task<Person> GetById(int id, CancellationToken cancellationToken);
    Task<IEnumerable<Person>> GetAll(CancellationToken cancellationToken);
    Task CreatePerson(Person person, CancellationToken cancellationToken);
    Task UpdatePerson(int id, Person person, CancellationToken cancellationToken);
    Task DeletePerson(int id, CancellationToken cancellationToken);
}