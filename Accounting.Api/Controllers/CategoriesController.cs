using Accounting.Application.Categories.Commands.Create;
using Accounting.Application.Categories.Commands.Delete;
using Accounting.Application.Categories.Commands.Update;
using Accounting.Application.Categories.Queries;
using Accounting.Application.Categories.Queries.List;
using Accounting.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;

namespace Accounting.Api.Controllers;

[Route("api/categories")]
[ApiController]
public class CategoriesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.Category.Read)]
    public async Task<ActionResult<PagedResult<CategoryDto>>> GetList(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        return Ok(await mediator.Send(new GetCategoriesQuery(search, page, pageSize), ct));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Category.Create)]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryCommand command, CancellationToken ct)
    {
        return Ok(await mediator.Send(command, ct));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Permissions.Category.Update)]
    public async Task<ActionResult<CategoryDto>> Update(int id, UpdateCategoryCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("ID mismatch");
        return Ok(await mediator.Send(command, ct));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Permissions.Category.Delete)]
    public async Task<ActionResult<bool>> Delete(int id, [FromQuery] string rowVersion, CancellationToken ct)
    {
        return Ok(await mediator.Send(new DeleteCategoryCommand(id, rowVersion), ct));
    }
}
