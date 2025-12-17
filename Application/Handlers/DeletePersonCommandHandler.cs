using Domain.Features;
using Infrastructure;
using Infrastructure.Interfaces;
using MediatR;

namespace Application.Handlers;

/// <summary>
/// Handler برای حذف Person.
/// از TransactionContext استفاده می‌کند برای اطمینان از atomicity.
/// </summary>
internal sealed class DeletePersonCommandHandler(
    IPersonRepository personRepository,
    ITransactionContext transactionContext) 
    : IRequestHandler<DeletePersonCommand, DeletePersonCommandResponse>
{
    public async Task<DeletePersonCommandResponse> Handle(
        DeletePersonCommand request, 
        CancellationToken cancellationToken)
    {
        // ✅ استفاده از Transaction برای اطمینان از atomicity
        await using var transaction = await transactionContext.BeginTransactionAsync(cancellationToken);
        
        try
        {
            await personRepository.DeletePerson(request.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            return new DeletePersonCommandResponse(true);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

