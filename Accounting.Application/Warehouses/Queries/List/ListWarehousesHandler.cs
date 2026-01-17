using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Models;
using Accounting.Application.Warehouses.Dto;
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Warehouses.Queries.List;

public class ListWarehousesHandler : IRequestHandler<ListWarehousesQuery, PagedResult<WarehouseListItemDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    
    public ListWarehousesHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }
    public async Task<PagedResult<WarehouseListItemDto>> Handle(ListWarehousesQuery r, CancellationToken ct)
    {
        IQueryable<Warehouse> q = _db.Warehouses
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService);

        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var s = r.Search.Trim().ToUpperInvariant();
            q = q.Where(x =>
                EF.Functions.Like(x.Code.ToUpper(), $"%{s}%") ||
                EF.Functions.Like(x.Name.ToUpper(), $"%{s}%"));
        }

        q = (r.Sort?.ToLowerInvariant()) switch
        {
            "code:asc" => q.OrderBy(x => x.Code),
            "code:desc" => q.OrderByDescending(x => x.Code),
            "name:asc" => q.OrderBy(x => x.Name),
            "name:desc" => q.OrderByDescending(x => x.Name),
            "isdefault:asc" => q.OrderBy(x => x.IsDefault).ThenBy(x => x.Name),
            "isdefault:desc" => q.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name),
            _ => q.OrderBy(x => x.Name)
        };

        var total = await q.CountAsync(ct);

        var items = await q
            .Skip((r.PageNumber - 1) * r.PageSize)
            .Take(r.PageSize)
            .Select(x => new WarehouseListItemDto(
                x.Id,
                x.BranchId,
                x.Code,
                x.Name,
                x.IsDefault,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            ))
            .ToListAsync(ct);

        return new PagedResult<WarehouseListItemDto>(total, r.PageNumber, r.PageSize, items, null);
    }
}
