using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Models;
using Accounting.Application.FixedAssets.Queries.Dto;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.FixedAssets.Queries.List;

public sealed class ListFixedAssetsHandler
    : IRequestHandler<ListFixedAssetsQuery, PagedResult<FixedAssetListItemDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public ListFixedAssetsHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<FixedAssetListItemDto>> Handle(
        ListFixedAssetsQuery r,
        CancellationToken ct)
    {
        var q = _db.FixedAssets
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService)
            .AsQueryable();

        if (!r.IncludeDeleted)
        {
            q = q.Where(x => !x.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var s = r.Search.Trim();
            q = q.Where(x =>
                x.Code.Contains(s) ||
                x.Name.Contains(s));
        }

        var total = await q.CountAsync(ct);

        var pageNumber = r.PageNumber < 1 ? 1 : r.PageNumber;
        var pageSize = r.PageSize <= 0 ? 20 : r.PageSize;

        q = q.OrderBy(x => x.Code).ThenBy(x => x.Id);

        var items = await q
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new FixedAssetListItemDto(
                x.Id,
                x.Code,
                x.Name,
                x.PurchaseDateUtc,
                x.PurchasePrice,
                x.UsefulLifeYears,
                x.DepreciationRatePercent,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            ))
            .ToListAsync(ct);

        return new PagedResult<FixedAssetListItemDto>(
            total,
            pageNumber,
            pageSize,
            items,
            null
        );
    }
}
