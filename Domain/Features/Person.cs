using Domain.Models;

using MediatR;

namespace Domain.Features;

public sealed record GetAllPersonQuery : IRequest<GetAllPersonQueryResponse>;
public sealed record GetAllPersonQueryResponse(IEnumerable<Person> Persons);

public sealed record GetPersonByIdQuery(int Id) : IRequest<GetPersonByIdQueryResponse>;
public sealed record GetPersonByIdQueryResponse(Person? Person);

public sealed record CreatePersonCommand(Person Person) : IRequest<CreatePersonCommandResponse>;
public sealed record CreatePersonCommandResponse(int Id);

public sealed record UpdatePersonCommand(int Id, Person Person) : IRequest<UpdatePersonCommandResponse>;
public sealed record UpdatePersonCommandResponse;

public sealed record DeletePersonCommand(int Id) : IRequest<DeletePersonCommandResponse>;
public sealed record DeletePersonCommandResponse(bool Success);