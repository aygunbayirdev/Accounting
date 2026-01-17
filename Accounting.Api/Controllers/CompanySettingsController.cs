using Accounting.Application.CompanySettings.Commands.Update;
using Accounting.Application.CompanySettings.Dto;
using Accounting.Application.CompanySettings.Queries.Get;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;

namespace Accounting.Api.Controllers;

[ApiController]
[Route("api/company-settings")]
public class CompanySettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanySettingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.CompanySettings.Read)]
    public async Task<ActionResult<CompanySettingsDetailDto>> Get()
    {
        var result = await _mediator.Send(new GetCompanySettingsQuery());
        return Ok(result);
    }

    [HttpPut]
    [Authorize(Policy = Permissions.CompanySettings.Update)]
    public async Task<ActionResult<CompanySettingsDetailDto>> Update(UpdateCompanySettingsCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
