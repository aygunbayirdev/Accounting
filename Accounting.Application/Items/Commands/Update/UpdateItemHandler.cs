// Accounting.Application/Items/Commands/Update/UpdateItemHandler.cs
using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;   // ConcurrencyConflictException
using Accounting.Application.Common.Extensions; // ApplyBranchFilter
using Accounting.Application.Common.Interfaces; // ICurrentUserService
using Accounting.Application.Common.Utils;
using Accounting.Application.Items.Queries.Dto;
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Items.Commands.Update;

public class UpdateItemHandler : IRequestHandler<UpdateItemCommand, ItemDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public UpdateItemHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<ItemDetailDto> Handle(UpdateItemCommand r, CancellationToken ct)
    {
        // (1) fetch (tracking)
        var e = await _db.Items
            .ApplyBranchFilter(_currentUserService)
            .FirstOrDefaultAsync(i => i.Id == r.Id && !i.IsDeleted, ct);
        if (e is null) throw new NotFoundException("Item", r.Id);

        // (3) original rowversion
        var original = Convert.FromBase64String(r.RowVersion);
        _db.Entry(e).Property(nameof(Item.RowVersion)).OriginalValue = original;

        // (4) normalize + parse
        e.CategoryId = r.CategoryId;
        e.Code = r.Code.Trim();
        e.Name = r.Name.Trim();
        e.Type = (Domain.Enums.ItemType)r.Type;
        e.Unit = r.Unit.Trim();
        e.VatRate = r.VatRate;
        e.DefaultWithholdingRate = r.DefaultWithholdingRate;

        if (r.PurchasePrice is null)
        {
            e.PurchasePrice = null;
        }
        else
        {
            Money.TryParse2(r.PurchasePrice, out var pp);
            e.PurchasePrice = Money.R2(pp);
        }

        if (r.SalesPrice is null)
        {
            e.SalesPrice = null;
        }
        else
        {
            Money.TryParse2(r.SalesPrice, out var sp);
            e.SalesPrice = Money.R2(sp);
        }

        // (6) save + concurrency
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("Item güncellemesinde eşzamanlılık hatası.");
        }

        // (7) fresh read
        // (7) fresh read
        var fresh = await _db.Items
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService)
            .Include(x => x.Category)
            .FirstAsync(x => x.Id == e.Id, ct);

        // (8) dto
        return new ItemDetailDto(
            fresh.Id,
            fresh.CategoryId,
            fresh.Category?.Name,
            fresh.Code,
            fresh.Name,
            (int)fresh.Type,
            fresh.Unit,
            fresh.VatRate,
            fresh.DefaultWithholdingRate ?? 0,
            fresh.PurchasePrice is null ? null : Money.S2(fresh.PurchasePrice.Value),
            fresh.SalesPrice is null ? null : Money.S2(fresh.SalesPrice.Value),
            Convert.ToBase64String(fresh.RowVersion),
            fresh.CreatedAtUtc,
            fresh.UpdatedAtUtc
        );
    }
}
