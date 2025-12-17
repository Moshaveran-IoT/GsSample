using Domain.Features;
using Infrastructure;
using MediatR;

namespace Application.Handlers;

/// <summary>
/// Handler برای دریافت Person با ID مشخص.
/// </summary>
internal sealed class GetPersonByIdQueryHandler(IPersonRepository personRepository) 
    : IRequestHandler<GetPersonByIdQuery, GetPersonByIdQueryResponse>
{
    public async Task<GetPersonByIdQueryResponse> Handle(
        GetPersonByIdQuery request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var person = await personRepository.GetById(request.Id, cancellationToken);
            return new GetPersonByIdQueryResponse(person);
        }
        catch (KeyNotFoundException)
        {
            return new GetPersonByIdQueryResponse(null);
        }
    }
}

