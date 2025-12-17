using Domain.Features;
using Domain.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Controller برای مدیریت Person.
/// از MediatR برای CQRS pattern استفاده می‌کند.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PersonController : ControllerBase
{
    private readonly IMediator _mediator;

    public PersonController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// دریافت همه Persons.
    /// GET: api/Person
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<GetAllPersonQueryResponse>> GetAll(CancellationToken cancellationToken)
    {
        var query = new GetAllPersonQuery();
        var response = await _mediator.Send(query, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// دریافت Person با ID مشخص.
    /// GET: api/Person/5
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<GetPersonByIdQueryResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var query = new GetPersonByIdQuery(id);
        var response = await _mediator.Send(query, cancellationToken);
        
        if (response.Person == null)
        {
            return NotFound($"Person with Id {id} not found");
        }
        
        return Ok(response);
    }

    /// <summary>
    /// ایجاد Person جدید.
    /// POST: api/Person
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreatePersonCommandResponse>> Create([FromBody] Person person, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var command = new CreatePersonCommand(person);
        var response = await _mediator.Send(command, cancellationToken);
        
        return CreatedAtAction(
            nameof(GetById),
            new { id = response.Id },
            response);
    }

    /// <summary>
    /// به‌روزرسانی Person موجود.
    /// PUT: api/Person/5
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<UpdatePersonCommandResponse>> Update(
        int id, 
        [FromBody] Person person, 
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var command = new UpdatePersonCommand(id, person);
        var response = await _mediator.Send(command, cancellationToken);
        
        return Ok(response);
    }

    /// <summary>
    /// حذف Person.
    /// DELETE: api/Person/5
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<DeletePersonCommandResponse>> Delete(int id, CancellationToken cancellationToken)
    {
        var command = new DeletePersonCommand(id);
        var response = await _mediator.Send(command, cancellationToken);
        
        if (!response.Success)
        {
            return NotFound($"Person with Id {id} not found");
        }
        
        return Ok(response);
    }
}
