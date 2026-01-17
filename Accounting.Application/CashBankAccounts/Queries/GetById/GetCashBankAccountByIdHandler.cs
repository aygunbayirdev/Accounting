using Accounting.Application.CashBankAccounts.Queries.Dto;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.CashBankAccounts.Queries.GetById;

public class GetCashBankAccountByIdHandler : IRequestHandler<GetCashBankAccountByIdQuery, CashBankAccountDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetCashBankAccountByIdHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }
    public async Task<CashBankAccountDetailDto> Handle(GetCashBankAccountByIdQuery r, CancellationToken ct)
    {
        var x = await _db.CashBankAccounts
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService)
            .FirstOrDefaultAsync(a => a.Id == r.Id && !a.IsDeleted, ct);
        if (x is null) throw new NotFoundException("CashBankAccount", r.Id);

        return new CashBankAccountDetailDto(
            x.Id,
            x.BranchId,
            x.Code,
            x.Type.ToString(),
            x.Name,
            x.Iban,
            x.Currency,
            x.Balance,
            Convert.ToBase64String(x.RowVersion),
            x.CreatedAtUtc,
            x.UpdatedAtUtc
        );
    }
}
