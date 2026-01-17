using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Utils;
using Accounting.Application.StockMovements.Queries.Dto;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.StockMovements.Queries.GetById;

public class GetStockMovementByIdHandler(IAppDbContext db) : IRequestHandler<GetStockMovementByIdQuery, StockMovementDetailDto>
{
    public async Task<StockMovementDetailDto> Handle(GetStockMovementByIdQuery r, CancellationToken ct)
    {
        var e = await db.StockMovements
            .AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == r.Id, ct);

        if (e is null) throw new NotFoundException("StockMovement", r.Id);

        return new StockMovementDetailDto(
            e.Id,
            e.BranchId,
            e.WarehouseId,
            e.Warehouse.Code,
            e.ItemId,
            e.Item.Code,
            e.Item.Name,
            e.Item.Unit,
            e.Type,
            e.Quantity,
            e.TransactionDateUtc,
            e.Note,
            Convert.ToBase64String(e.RowVersion),
            e.CreatedAtUtc,
            e.UpdatedAtUtc
        );
    }
}
