using Domain.Features;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interfaces;
using MediatR;

namespace Application.Handlers;

/// <summary>
/// Handler برای ایجاد Person جدید.
/// از TransactionContext استفاده می‌کند برای اطمینان از atomicity.
/// </summary>
internal sealed class CreatePersonCommandHandler(
    IPersonRepository personRepository,
    ITransactionContext transactionContext) 
    : IRequestHandler<CreatePersonCommand, CreatePersonCommandResponse>
{
    public async Task<CreatePersonCommandResponse> Handle(
        CreatePersonCommand request, 
        CancellationToken cancellationToken)
    {
        // ✅ استفاده از Transaction برای اطمینان از atomicity
        await using var transaction = await transactionContext.BeginTransactionAsync(cancellationToken);
        
        try
        {
            await personRepository.CreatePerson(request.Person, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            return new CreatePersonCommandResponse(request.Person.Id);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

