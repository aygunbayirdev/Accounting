using Accounting.Application.Common.Models;
using Accounting.Application.FixedAssets.Commands.Create; 
using Accounting.Application.FixedAssets.Commands.Delete; 
using Accounting.Application.FixedAssets.Commands.Update; 
using Accounting.Application.FixedAssets.Queries.Dto;
using Accounting.Application.FixedAssets.Queries.GetById;
using Accounting.Application.FixedAssets.Queries.List;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;

namespace Accounting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class FixedAssetsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FixedAssetsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GET api/FixedAssets
    [HttpGet]
    [Authorize(Policy = Permissions.FixedAsset.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<FixedAssetListItemDto>>> List(
        [FromQuery] ListFixedAssetsQuery query,
        CancellationToken ct)
    {
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    // GET api/FixedAssets/5
    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.FixedAsset.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FixedAssetDetailDto>> GetById(
        [FromRoute] int id,
        CancellationToken ct)
    {
        if (id <= 0) return BadRequest();

        var result = await _mediator.Send(new GetFixedAssetByIdQuery(id), ct);
        return Ok(result);
    }

    // POST api/FixedAssets
    [HttpPost]            
    [Authorize(Policy = Permissions.FixedAsset.Create)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FixedAssetDetailDto>> Create(
        [FromBody] CreateFixedAssetCommand command,
        CancellationToken ct)                    
    {                                            
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(                  
            nameof(GetById),                     
            new { id = result.Id },              
            result);                             
    }                                            

    // PUT api/FixedAssets/5  
    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.FixedAsset.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FixedAssetDetailDto>> Update(
        [FromRoute] int id,                       
        [FromBody] UpdateFixedAssetCommand command,
        CancellationToken ct)                    
    {                                            
        if (id != command.Id)                    
            return BadRequest();                 

        var result = await _mediator.Send(command, ct);
        return Ok(result);                       
    }                                            

    // DELETE api/FixedAssets/5
    [HttpDelete("{id:int}")]   
    [Authorize(Policy = Permissions.FixedAsset.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(
        [FromRoute] int id,    
        [FromBody] DeleteFixedAssetCommand command,
        CancellationToken ct)  
    {                          
        if (id != command.Id)  
            return BadRequest();

        await _mediator.Send(command, ct);  
        return NoContent();    
    }                          
}
