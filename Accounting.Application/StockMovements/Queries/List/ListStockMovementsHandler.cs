using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Models;
using Accounting.Application.Common.Utils;
using Accounting.Application.StockMovements.Queries.Dto;
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.StockMovements.Queries.List;

public class ListStockMovementsHandler : IRequestHandler<ListStockMovementsQuery, PagedResult<StockMovementDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    
    public ListStockMovementsHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }
    public async Task<PagedResult<StockMovementDto>> Handle(ListStockMovementsQuery r, CancellationToken ct)
    {
        IQueryable<StockMovement> q = _db.StockMovements
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService);

        // Include'ları IQueryable üstünden ekle (CS0266 fix)
        q = q
            .Include(x => x.Warehouse)
            .Include(x => x.Item);

        if (r.WarehouseId is not null) q = q.Where(x => x.WarehouseId == r.WarehouseId);
        if (r.ItemId is not null) q = q.Where(x => x.ItemId == r.ItemId);
        if (r.Type is not null) q = q.Where(x => x.Type == r.Type);

        if (r.FromUtc is not null) q = q.Where(x => x.TransactionDateUtc >= r.FromUtc);
        if (r.ToUtc is not null) q = q.Where(x => x.TransactionDateUtc <= r.ToUtc);

        q = (r.Sort?.ToLowerInvariant()) switch
        {
            "date:asc" => q.OrderBy(x => x.TransactionDateUtc).ThenBy(x => x.Id),
            "date:desc" => q.OrderByDescending(x => x.TransactionDateUtc).ThenByDescending(x => x.Id),
            "created:asc" => q.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id),
            "created:desc" => q.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id),
            "item:asc" => q.OrderBy(x => x.Item.Name).ThenByDescending(x => x.TransactionDateUtc),
            "item:desc" => q.OrderByDescending(x => x.Item.Name).ThenByDescending(x => x.TransactionDateUtc),
            _ => q.OrderByDescending(x => x.TransactionDateUtc).ThenByDescending(x => x.Id)
        };

        var total = await q.CountAsync(ct);

        var items = await q
            .Skip((r.PageNumber - 1) * r.PageSize)
            .Take(r.PageSize)
            .Select(x => new StockMovementDto(
                x.Id,
                x.BranchId,
                x.WarehouseId,
                x.Warehouse.Code,
                x.ItemId,
                x.Item.Code,
                x.Item.Name,
                x.Item.Unit,
                x.Type,
                x.Quantity,
                x.TransactionDateUtc,
                x.Note,
                Convert.ToBase64String(x.RowVersion),
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            ))
            .ToListAsync(ct);

        return new PagedResult<StockMovementDto>(
            total,
            r.PageNumber,
            r.PageSize,
            items,
            null
        );
    }
}
