using Accounting.Application.CashBankAccounts.Queries.Dto;
using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions; // ApplyBranchFilter
using Accounting.Application.Common.Interfaces; // ICurrentUserService
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.CashBankAccounts.Commands.Update;

public class UpdateCashBankAccountHandler : IRequestHandler<UpdateCashBankAccountCommand, CashBankAccountDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public UpdateCashBankAccountHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<CashBankAccountDetailDto> Handle(UpdateCashBankAccountCommand r, CancellationToken ct)
    {
        var e = await _db.CashBankAccounts
            .ApplyBranchFilter(_currentUserService)
            .FirstOrDefaultAsync(a => a.Id == r.Id && !a.IsDeleted, ct);
        if (e is null) throw new NotFoundException("CashBankAccount", r.Id);

        _db.Entry(e).Property(nameof(CashBankAccount.RowVersion)).OriginalValue = Convert.FromBase64String(r.RowVersion);

        e.Type = r.Type;                                 // <-- doğrudan enum
        e.Name = r.Name.Trim();
        e.Iban = string.IsNullOrWhiteSpace(r.Iban) ? null : r.Iban.Trim();

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("Hesap başka biri tarafından güncellendi.");
        }

        var fresh = await _db.CashBankAccounts
            .AsNoTracking()
            .FirstAsync(x => x.Id == r.Id, ct);

        return new CashBankAccountDetailDto(
            fresh.Id,
            fresh.BranchId,
            fresh.Code,
            fresh.Type.ToString(),
            fresh.Name,
            fresh.Iban,
            fresh.Currency,
            fresh.Balance,
            Convert.ToBase64String(fresh.RowVersion),
            fresh.CreatedAtUtc,
            fresh.UpdatedAtUtc
        );
    }
}
