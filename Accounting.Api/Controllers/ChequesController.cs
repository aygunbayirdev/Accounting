using Accounting.Application.Cheques.Commands.Create;
using Accounting.Application.Cheques.Commands.UpdateStatus;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;

namespace Accounting.Api.Controllers;

[Route("api/cheques")]
[ApiController]
public class ChequesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Permissions.Cheque.Create)]
    public async Task<ActionResult<int>> Create(CreateChequeCommand command, CancellationToken ct)
    {
        return Ok(await mediator.Send(command, ct));
    }

    [HttpPut("{id}/status")]
    [Authorize(Policy = Permissions.Cheque.UpdateStatus)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
    {
        var command = new UpdateChequeStatusCommand(id, request.NewStatus, request.TransactionDate, request.CashBankAccountId);
        await mediator.Send(command, ct);
        return NoContent();
    }
}

public record UpdateStatusRequest(ChequeStatus NewStatus, DateTime? TransactionDate, int? CashBankAccountId);
