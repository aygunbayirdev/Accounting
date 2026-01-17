using Accounting.Application.Branches.Commands.Create;
using Accounting.Application.Branches.Commands.Delete;
using Accounting.Application.Branches.Commands.Update;
using Accounting.Application.Branches.Queries.Dto;
using Accounting.Application.Branches.Queries.GetById;
using Accounting.Application.Branches.Queries.List;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;

namespace Accounting.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BranchesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public BranchesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // GET /api/branches
        [HttpGet]
        [Authorize(Policy = Permissions.Branch.Read)]
        [ProducesResponseType(typeof(IReadOnlyList<BranchListItemDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<BranchListItemDto>>> List(CancellationToken ct)
        {
            var res = await _mediator.Send(new ListBranchesQuery(), ct);
            return Ok(res);
        }
        // GET /api/branches/{id}
        [HttpGet("{id}")]
        [Authorize(Policy = Permissions.Branch.Read)]
        [ProducesResponseType(typeof(BranchDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BranchDetailDto>> GetById(int id)
        {
            var result = await _mediator.Send(new GetBranchByIdQuery(id));
            return Ok(result);
        }

        // POST /api/branches
        [HttpPost]
        [Authorize(Policy = Permissions.Branch.Create)]
        [ProducesResponseType(typeof(BranchDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BranchDetailDto>> Create(CreateBranchCommand command)
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        // PUT /api/branches/{id}
        [HttpPut("{id}")]
        [Authorize(Policy = Permissions.Branch.Update)]
        [ProducesResponseType(typeof(BranchDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<BranchDetailDto>> Update(int id, UpdateBranchCommand command)
        {
            if (id != command.Id) return BadRequest();
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        // DELETE /api/branches/{id}
        [HttpDelete("{id}")]
        [Authorize(Policy = Permissions.Branch.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id, [FromQuery] string rowVersion)
        {
             await _mediator.Send(new DeleteBranchCommand(id, rowVersion));
             return NoContent();
        }
    }
}
