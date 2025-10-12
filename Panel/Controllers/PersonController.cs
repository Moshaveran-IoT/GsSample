using Domain.Features;
using Domain.Models;

using MediatR;

using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("[controller]")]
public class PersonController : ControllerBase
{
    private readonly IMediator _mediator;

    public PersonController(IMediator mediator)
        => this._mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await this._mediator.Send(new GetAllPersonQuery(), cancellationToken);
        return this.Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await this._mediator.Send(new GetPersonByIdQuery(id), cancellationToken);
        return result is not null ? this.Ok(result) : this.NotFound();
    }
    
    [HttpPost]
    public async Task<IActionResult> Create(Person person, CancellationToken cancellationToken)
    {
        var result = await this._mediator.Send(new CreatePersonCommand(person), cancellationToken);
        return this.CreatedAtAction(nameof(this.GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Person person, CancellationToken cancellationToken)
    {
        await this._mediator.Send(new UpdatePersonCommand(id, person), cancellationToken);
        return this.NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await this._mediator.Send(new DeletePersonCommand(id), cancellationToken);
        return result.Success ? this.NoContent() : this.NotFound();
    }
}