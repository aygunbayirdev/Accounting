using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Warehouses.Dto;
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

using Accounting.Application.Common.Interfaces;

namespace Accounting.Application.Warehouses.Commands.Create;

public class CreateWarehouseHandler(IAppDbContext db, ICurrentUserService currentUserService)
    : IRequestHandler<CreateWarehouseCommand, WarehouseDetailDto>
{
    public async Task<WarehouseDetailDto> Handle(CreateWarehouseCommand r, CancellationToken ct)
    {
        var branchId = currentUserService.BranchId ?? throw new UnauthorizedAccessException();
        var code = r.Code.Trim().ToUpperInvariant();
        var name = r.Name.Trim();

        // aynı şubede aynı code olmasın (soft delete hariç)
        var exists = await db.Warehouses.AnyAsync(x =>
            x.BranchId == branchId &&
            x.Code == code, ct);

        if (exists)
            throw new BusinessRuleException($"Aynı şubede '{code}' kodlu depo zaten var.");

        // IsDefault true ise aynı şubedeki diğer default'ları false yap (TR'de pratik)
        if (r.IsDefault)
        {
            var defaults = await db.Warehouses
                .Where(x => x.BranchId == branchId && x.IsDefault)
                .ToListAsync(ct);

            foreach (var d in defaults) d.IsDefault = false;
        }

        var e = new Warehouse
        {
            BranchId = branchId,
            Code = code,
            Name = name,
            IsDefault = r.IsDefault
        };

        db.Warehouses.Add(e);

        await db.SaveChangesAsync(ct);

        // fresh read (rowversion)
        var saved = await db.Warehouses.AsNoTracking().FirstAsync(x => x.Id == e.Id, ct);

        return new WarehouseDetailDto(
            saved.Id,
            saved.BranchId,
            saved.Code,
            saved.Name,
            saved.IsDefault,
            Convert.ToBase64String(saved.RowVersion),
            saved.CreatedAtUtc,
            saved.UpdatedAtUtc
        );
    }
}
