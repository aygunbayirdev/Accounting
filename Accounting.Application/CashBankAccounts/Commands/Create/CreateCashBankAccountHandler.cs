using Accounting.Application.CashBankAccounts.Queries.Dto;
using Accounting.Application.Common.Abstractions;
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.CashBankAccounts.Commands.Create;

public class CreateCashBankAccountHandler(IAppDbContext db)
    : IRequestHandler<CreateCashBankAccountCommand, CashBankAccountDetailDto>
{
    public async Task<CashBankAccountDetailDto> Handle(CreateCashBankAccountCommand r, CancellationToken ct)
    {
        // Auto-generate Code: CBA-{BranchId}-{Sequence}
        var code = await GenerateCodeAsync(r.BranchId, ct);

        var e = new CashBankAccount
        {
            BranchId = r.BranchId,
            Code = code,
            Type = r.Type,
            Name = r.Name.Trim(),
            Iban = string.IsNullOrWhiteSpace(r.Iban) ? null : r.Iban.Trim()
        };

        db.CashBankAccounts.Add(e);
        await db.SaveChangesAsync(ct);

        var fresh = await db.CashBankAccounts.AsNoTracking().FirstAsync(x => x.Id == e.Id, ct);

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

    private async Task<string> GenerateCodeAsync(int branchId, CancellationToken ct)
    {
        var lastCode = await db.CashBankAccounts
            .IgnoreQueryFilters()
            .Where(c => c.BranchId == branchId)
            .OrderByDescending(c => c.Id)
            .Select(c => c.Code)
            .FirstOrDefaultAsync(ct);

        int nextSequence = 1;

        if (!string.IsNullOrEmpty(lastCode))
        {
            var parts = lastCode.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var lastSeq))
            {
                nextSequence = lastSeq + 1;
            }
        }

        return $"CBA-{branchId}-{nextSequence:D5}";
    }
}
