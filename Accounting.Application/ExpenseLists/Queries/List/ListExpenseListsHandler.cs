using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Models;
using Accounting.Application.Common.Utils;
using Accounting.Application.ExpenseLists.Dto;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.ExpenseLists.Queries.List;

public class ListExpenseListsHandler : IRequestHandler<ListExpenseListsQuery, PagedResult<ExpenseListDetailDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    
    public ListExpenseListsHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<ExpenseListDetailDto>> Handle(ListExpenseListsQuery q, CancellationToken ct)
    {
        var query = _db.ExpenseLists
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService)
            .Where(x => !x.IsDeleted);

        // Filters
        if (q.BranchId.HasValue)
            query = query.Where(x => x.BranchId == q.BranchId.Value);

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            if (Enum.TryParse<ExpenseListStatus>(q.Status, true, out var status))
                query = query.Where(x => x.Status == status);
        }

        // Sort
        var sort = (q.Sort ?? "createdAtUtc:desc").Split(':');
        var field = sort[0].ToLowerInvariant();
        var dir = sort.Length > 1 ? sort[1].ToLowerInvariant() : "desc";

        query = (field, dir) switch
        {
            ("createdat" or "createdatutc", "asc") => query.OrderBy(x => x.CreatedAtUtc),
            ("createdat" or "createdatutc", _) => query.OrderByDescending(x => x.CreatedAtUtc),
            ("name", "asc") => query.OrderBy(x => x.Name),
            ("name", _) => query.OrderByDescending(x => x.Name),
            ("status", "asc") => query.OrderBy(x => x.Status),
            ("status", _) => query.OrderByDescending(x => x.Status),
            _ => query.OrderByDescending(x => x.CreatedAtUtc)
        };

        // Count
        var total = await query.CountAsync(ct);

        // Page
        var items = await query
            .Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(x => new ExpenseListDetailDto(
                x.Id,
                x.BranchId,
                x.Name,
                x.Status.ToString(),
                new List<ExpenseLineDto>(),
                DecimalExtensions.RoundAmount(x.Lines.Sum(x => x.Amount)),
                Convert.ToBase64String(x.RowVersion),
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            ))
            .ToListAsync(ct);

        return new PagedResult<ExpenseListDetailDto>(
            Items: items,
            Total: total,
            PageNumber: q.PageNumber,
            PageSize: q.PageSize
        );
    }
}