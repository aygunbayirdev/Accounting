using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Models;
using Accounting.Application.Common.Utils;
using Accounting.Application.Stocks.Queries.Dto;
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Stocks.Queries.List;

public class ListStocksHandler(IAppDbContext db, ICurrentUserService currentUserService)
    : IRequestHandler<ListStocksQuery, PagedResult<StockListItemDto>>
{
    private readonly IAppDbContext _db = db;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<PagedResult<StockListItemDto>> Handle(ListStocksQuery r, CancellationToken ct)
    {
        IQueryable<Stock> q = _db.Stocks
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService);

        // Include'ları IQueryable üstünden ekle (CS0266 fix)
        q = q
            .Include(x => x.Warehouse)
            .Include(x => x.Item);

        if (r.WarehouseId is not null)
            q = q.Where(x => x.WarehouseId == r.WarehouseId);

        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var s = r.Search.Trim().ToUpperInvariant();
            q = q.Where(x =>
                EF.Functions.Like(x.Item.Code.ToUpper(), $"%{s}%") ||
                EF.Functions.Like(x.Item.Name.ToUpper(), $"%{s}%"));
        }

        q = (r.Sort?.ToLowerInvariant()) switch
        {
            "itemcode:asc" => q.OrderBy(x => x.Item.Code),
            "itemcode:desc" => q.OrderByDescending(x => x.Item.Code),
            "itemname:asc" => q.OrderBy(x => x.Item.Name),
            "itemname:desc" => q.OrderByDescending(x => x.Item.Name),
            "qty:asc" => q.OrderBy(x => x.Quantity).ThenBy(x => x.Item.Name),
            "qty:desc" => q.OrderByDescending(x => x.Quantity).ThenBy(x => x.Item.Name),
            _ => q.OrderBy(x => x.Item.Name)
        };

        var total = await q.CountAsync(ct);

        var items = await q
            .Skip((r.PageNumber - 1) * r.PageSize)
            .Take(r.PageSize)
            .Select(x => new StockListItemDto(
                x.Id,
                x.BranchId,
                x.WarehouseId,
                x.Warehouse.Code,
                x.ItemId,
                x.Item.Code,
                x.Item.Name,
                x.Item.Unit,
                x.Quantity,
                Convert.ToBase64String(x.RowVersion),
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            ))
            .ToListAsync(ct);

        return new PagedResult<StockListItemDto>(
            total,
            r.PageNumber,
            r.PageSize,
            items,
            null
        );
    }
}
