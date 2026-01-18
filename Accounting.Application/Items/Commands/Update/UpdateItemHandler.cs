using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Items.Queries.Dto;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Data;

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
        var item = await _db.Items
            .ApplyBranchFilter(_currentUserService)
            .Include(x => x.Category)
            .Where(x => x.Id == r.Id && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (item == null)
            throw new NotFoundException("Item", r.Id);

        var providedVersion = Convert.FromBase64String(r.RowVersion);
        if (!item.RowVersion.SequenceEqual(providedVersion))
            throw new ConcurrencyConflictException("Item");

        item.CategoryId = r.CategoryId;
        item.Code = r.Code.Trim();
        item.Name = r.Name.Trim();
        item.Type = (ItemType)r.Type;
        item.Unit = r.Unit.Trim();
        item.VatRate = r.VatRate;
        item.DefaultWithholdingRate = r.DefaultWithholdingRate;
        item.PurchasePrice = r.PurchasePrice;
        item.SalesPrice = r.SalesPrice;
        item.PurchaseAccountCode = r.PurchaseAccountCode?.Trim();
        item.SalesAccountCode = r.SalesAccountCode?.Trim();
        item.UsefulLifeYears = r.UsefulLifeYears;

        await _db.SaveChangesAsync(ct);

        return new ItemDetailDto(
            item.Id,
            item.CategoryId,
            item.Category?.Name,
            item.Code,
            item.Name,
            (int)item.Type,
            item.Unit,
            item.VatRate,
            item.DefaultWithholdingRate ?? 0,
            item.PurchasePrice,
            item.SalesPrice,
            item.PurchaseAccountCode,
            item.SalesAccountCode,
            item.UsefulLifeYears,
            Convert.ToBase64String(item.RowVersion),
            item.CreatedAtUtc,
            item.UpdatedAtUtc
        );
    }
}
