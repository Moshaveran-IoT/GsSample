using Domain.Features;
using Infrastructure;
using Infrastructure.Interfaces;
using MediatR;

namespace Application.Handlers;

/// <summary>
/// Handler برای به‌روزرسانی Person موجود.
/// از TransactionContext استفاده می‌کند برای اطمینان از atomicity.
/// </summary>
internal sealed class UpdatePersonCommandHandler(
    IPersonRepository personRepository,
    ITransactionContext transactionContext) 
    : IRequestHandler<UpdatePersonCommand, UpdatePersonCommandResponse>
{
    public async Task<UpdatePersonCommandResponse> Handle(
        UpdatePersonCommand request, 
        CancellationToken cancellationToken)
    {
        // ✅ استفاده از Transaction برای اطمینان از atomicity
        await using var transaction = await transactionContext.BeginTransactionAsync(cancellationToken);
        
        try
        {
            await personRepository.UpdatePerson(request.Id, request.Person, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            return new UpdatePersonCommandResponse();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

