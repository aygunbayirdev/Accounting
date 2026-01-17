using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Categories.Queries.List;

public class ListCategoriesHandler(IAppDbContext db) : IRequestHandler<ListCategoriesQuery, PagedResult<CategoryListItemDto>>
{
    public async Task<PagedResult<CategoryListItemDto>> Handle(ListCategoriesQuery r, CancellationToken ct)
    {
        var query = db.Categories
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            query = query.Where(x => x.Name.Contains(r.Search));
        }

        var totalCount = await query.CountAsync(ct);

        var categories = await query
            .OrderBy(x => x.Name)
            .Skip((r.Page - 1) * r.PageSize)
            .Take(r.PageSize)
            .ToListAsync(ct);

        var items = categories.Select(c => new CategoryListItemDto(
            c.Id,
            c.Name,
            c.Description,
            c.Color,
            c.CreatedAtUtc,
            c.UpdatedAtUtc
        )).ToList();

        return new PagedResult<CategoryListItemDto>(totalCount, r.Page, r.PageSize, items);
    }
}
