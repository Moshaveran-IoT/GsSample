using Domain.Features;

using Infrastructure;

using MediatR;

namespace Application;

internal sealed class GetAllPersonQueryHandler(IPersonRepository personRepository) : IRequestHandler<GetAllPersonQuery, GetAllPersonQueryResponse>
{
    public async Task<GetAllPersonQueryResponse> Handle(GetAllPersonQuery request, CancellationToken cancellationToken)
    {
        var persons = await personRepository.GetAll(cancellationToken);
        return new GetAllPersonQueryResponse(persons);
    }
}
