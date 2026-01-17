using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Warehouses.Dto;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Warehouses.Queries.GetById;

public class GetWarehouseByIdHandler : IRequestHandler<GetWarehouseByIdQuery, WarehouseDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    
    public GetWarehouseByIdHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }
    public async Task<WarehouseDetailDto> Handle(GetWarehouseByIdQuery r, CancellationToken ct)
    {
        var e = await _db.Warehouses
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService)
            .FirstOrDefaultAsync(x => x.Id == r.Id, ct);

        if (e is null) throw new NotFoundException("Warehouse", r.Id);

        return new WarehouseDetailDto(
            e.Id,
            e.BranchId,
            e.Code,
            e.Name,
            e.IsDefault,
            Convert.ToBase64String(e.RowVersion),
            e.CreatedAtUtc,
            e.UpdatedAtUtc
        );
    }
}
