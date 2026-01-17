using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Models;
using Accounting.Application.ExpenseDefinitions.Queries.Dto;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.ExpenseDefinitions.Queries.List;

public sealed class ListExpenseDefinitionsHandler
    : IRequestHandler<ListExpenseDefinitionsQuery, PagedResult<ExpenseDefinitionListItemDto>>
{
    private readonly IAppDbContext _db;

    public ListExpenseDefinitionsHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ExpenseDefinitionListItemDto>> Handle(
        ListExpenseDefinitionsQuery r,
        CancellationToken ct)
    {
        var q = _db.ExpenseDefinitions.AsNoTracking().AsQueryable();

        // OnlyActive filtresi
        if (r.OnlyActive is true)
        {
            q = q.Where(x => x.IsActive);
        }
        else if (r.OnlyActive is false)
        {
            q = q.Where(x => !x.IsActive);
        }

        // Search: Code veya Name contains (case-insensitive)
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var s = r.Search.Trim();
            q = q.Where(x =>
                x.Code.Contains(s) ||
                x.Name.Contains(s));
            // İstersen burada ToLower() ile case-insensitive genişletebilirsin;
            // şimdilik SQL collation’a bırakıyoruz.
        }

        // Toplam kayıt
        var total = await q.CountAsync(ct);

        // Sayfalama
        var pageNumber = r.PageNumber < 1 ? 1 : r.PageNumber;
        var pageSize = r.PageSize <= 0 ? 20 : r.PageSize;

        q = q.OrderBy(x => x.Code)
             .ThenBy(x => x.Id);

        var items = await q
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ExpenseDefinitionListItemDto(
                x.Id,
                x.Code,
                x.Name,
                x.DefaultVatRate,
                x.IsActive,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            ))
            .ToListAsync(ct);

        return new PagedResult<ExpenseDefinitionListItemDto>(
            total,
            pageNumber,
            pageSize,
            items,
            null // Totals yok
        );
    }
}
