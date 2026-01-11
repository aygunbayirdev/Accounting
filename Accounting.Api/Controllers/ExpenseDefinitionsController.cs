using Accounting.Application.Common.Models;
using Accounting.Application.ExpenseDefinitions.Commands.Create;
using Accounting.Application.ExpenseDefinitions.Commands.Update;
using Accounting.Application.ExpenseDefinitions.Commands.Delete;
using Accounting.Application.ExpenseDefinitions.Queries.Dto;
using Accounting.Application.ExpenseDefinitions.Queries.GetById;
using Accounting.Application.ExpenseDefinitions.Queries.List;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;

namespace Accounting.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExpenseDefinitionsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ExpenseDefinitionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // GET api/ExpenseDefinitions
        [HttpGet]
        [Authorize(Policy = Permissions.ExpenseDefinition.Read)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<ExpenseDefinitionListItemDto>>> List(
            [FromQuery] ListExpenseDefinitionsQuery query,
            CancellationToken ct)
        {
            var res = await _mediator.Send(query, ct);
            return Ok(res);
        }

        // GET api/ExpenseDefinitions/5
        [HttpGet("{id:int}")]
        [Authorize(Policy = Permissions.ExpenseDefinition.Read)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ExpenseDefinitionDetailDto>> GetById(
            [FromRoute] int id,
            CancellationToken ct)
        {
            if (id <= 0) return BadRequest();

            var res = await _mediator.Send(new GetExpenseDefinitionByIdQuery(id), ct);
            return Ok(res);
        }

        // POST api/ExpenseDefinitions
        [HttpPost]
        [Authorize(Policy = Permissions.ExpenseDefinition.Create)]
        public async Task<IActionResult> Create([FromBody] CreateExpenseDefinitionCommand command, CancellationToken ct)
        {
            var id = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }

        // PUT api/ExpenseDefinitions/5
        [HttpPut("{id}")]
        [Authorize(Policy = Permissions.ExpenseDefinition.Update)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateExpenseDefinitionCommand command, CancellationToken ct)
        {
            if (id != command.Id)
                return BadRequest("ID mismatch");

            await _mediator.Send(command, ct);
            return NoContent();
        }

        // DELETE api/ExpenseDefinitions/5
        [HttpDelete("{id}")]
        [Authorize(Policy = Permissions.ExpenseDefinition.Delete)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            await _mediator.Send(new SoftDeleteExpenseDefinitionCommand(id), ct);
            return NoContent();
        }
    }
}
