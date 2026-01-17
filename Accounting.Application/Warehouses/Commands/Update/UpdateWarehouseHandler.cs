using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions; // ApplyBranchFilter
using Accounting.Application.Common.Interfaces; // ICurrentUserService
using Accounting.Application.Warehouses.Dto;
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Warehouses.Commands.Update;

public class UpdateWarehouseHandler : IRequestHandler<UpdateWarehouseCommand, WarehouseDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public UpdateWarehouseHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<WarehouseDetailDto> Handle(UpdateWarehouseCommand r, CancellationToken ct)
    {
        var e = await _db.Warehouses
            .ApplyBranchFilter(_currentUserService)
            .FirstOrDefaultAsync(x => x.Id == r.Id, ct);

        if (e is null) throw new NotFoundException("Warehouse", r.Id);

        // concurrency: RowVersion original set
        e.RowVersion = e.RowVersion; // no-op (avoid analyzer)
        _db.Entry(e).Property(nameof(Warehouse.RowVersion)).OriginalValue = Convert.FromBase64String(r.RowVersion);

        var code = r.Code.Trim().ToUpperInvariant();
        var name = r.Name.Trim();

        // aynı şubede code çakışması
        var exists = await _db.Warehouses.AnyAsync(x =>
            x.Id != r.Id &&
            x.BranchId == r.BranchId &&
            x.Code == code, ct);

        if (exists)
            throw new BusinessRuleException($"Aynı şubede '{code}' kodlu depo zaten var.");

        // BranchId değişimi istemiyorsan burada kilitleyebilirsin.
        // Şimdilik input ile set ediyorum ama genelde depo şubesi değişmez.
        e.BranchId = r.BranchId;
        e.Code = code;
        e.Name = name;

        if (r.IsDefault && !e.IsDefault)
        {
            var defaults = await _db.Warehouses
                .Where(x => x.BranchId == r.BranchId && x.IsDefault)
                .ToListAsync(ct);

            foreach (var d in defaults) d.IsDefault = false;
        }

        e.IsDefault = r.IsDefault;
        e.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("Depo güncellenirken eşzamanlılık hatası oluştu.");
        }

        var saved = await _db.Warehouses.AsNoTracking().FirstAsync(x => x.Id == r.Id, ct);

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
