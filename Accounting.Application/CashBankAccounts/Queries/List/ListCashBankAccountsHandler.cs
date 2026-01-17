using Accounting.Application.CashBankAccounts.Queries.Dto;
using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Constants;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Models;
using Accounting.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.CashBankAccounts.Queries.List;

public class ListCashBankAccountsHandler : IRequestHandler<ListCashBankAccountsQuery, PagedResult<CashBankAccountListItemDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public ListCashBankAccountsHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }
    public async Task<PagedResult<CashBankAccountListItemDto>> Handle(ListCashBankAccountsQuery q, CancellationToken ct)
    {
        // Normalize pagination
        var pageNumber = PaginationConstants.NormalizePage(q.PageNumber);
        var pageSize = PaginationConstants.NormalizePageSize(q.PageSize);

        var query = _db.CashBankAccounts
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService)
            .Where(x => !x.IsDeleted);

        // BranchId filter
        if (q.BranchId.HasValue)
            query = query.Where(x => x.BranchId == q.BranchId.Value);

        // Type filtresi: Any -> filtre yok; Cash/Bank -> eşitlik
        if (q.Type == CashBankAccountTypeFilter.Cash)
            query = query.Where(x => x.Type == CashBankAccountType.Cash);
        else if (q.Type == CashBankAccountTypeFilter.Bank)
            query = query.Where(x => x.Type == CashBankAccountType.Bank);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToUpperInvariant();
            query = query.Where(x => EF.Functions.Like(x.Name.ToUpper(), $"%{s}%")
                                  || EF.Functions.Like(x.Code.ToUpper(), $"%{s}%"));
        }

        if (!string.IsNullOrWhiteSpace(q.IbanStartsWith))
        {
            var pfx = q.IbanStartsWith.Trim();
            query = query.Where(x => x.Iban != null && x.Iban.StartsWith(pfx));
        }

        query = (q.Sort?.ToLowerInvariant()) switch
        {
            "name:desc" => query.OrderByDescending(x => x.Name),
            "type:asc" => query.OrderBy(x => x.Type).ThenBy(x => x.Name),
            "type:desc" => query.OrderByDescending(x => x.Type).ThenBy(x => x.Name),
            _ => query.OrderBy(x => x.Name)
        };

        var total = await query.CountAsync(ct);

        var items = await query.Skip((pageNumber - 1) * pageSize)
                               .Take(pageSize)
                               .Select(x => new CashBankAccountListItemDto(
                                   x.Id,
                                   x.BranchId,
                                   x.Code,
                                   x.Type.ToString(),
                                   x.Name,
                                   x.Iban,
                                   x.Currency,
                                   x.Balance,
                                   x.CreatedAtUtc,
                                   x.UpdatedAtUtc
                               ))
                               .ToListAsync(ct);

        return new PagedResult<CashBankAccountListItemDto>(total, pageNumber, pageSize, items, null);
    }
}
