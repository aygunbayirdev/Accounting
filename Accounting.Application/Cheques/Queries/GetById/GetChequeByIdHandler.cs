using Accounting.Application.Cheques.Queries.Dto;
using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Cheques.Queries.GetById;

public class GetChequeByIdHandler : IRequestHandler<GetChequeByIdQuery, ChequeDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetChequeByIdHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<ChequeDetailDto> Handle(GetChequeByIdQuery request, CancellationToken ct)
    {
        var cheque = await _db.Cheques
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService)
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);

        if (cheque is null)
            throw new NotFoundException("Cheque", request.Id);

        return new ChequeDetailDto(
            cheque.Id,
            cheque.BranchId,
            cheque.ChequeNumber,
            cheque.Type.ToString(),
            cheque.Amount,
            cheque.DueDate,
            cheque.DrawerName,
            cheque.BankName,
            cheque.Status.ToString(),
            cheque.CreatedAtUtc,
            cheque.UpdatedAtUtc
        );
    }
}
