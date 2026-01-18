using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Items.Queries.Dto;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Items.Commands.Create;

public class CreateItemHandler : IRequestHandler<CreateItemCommand, ItemDetailDto>
{
    private readonly IAppDbContext _db;

    public CreateItemHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ItemDetailDto> Handle(CreateItemCommand r, CancellationToken ct)
    {

        var item = new Item
        {
            CategoryId = r.CategoryId,
            Code = r.Code.Trim(),
            Name = r.Name.Trim(),
            Type = (ItemType)r.Type,
            Unit = r.Unit.Trim(),
            VatRate = r.VatRate,
            DefaultWithholdingRate = r.DefaultWithholdingRate,
            PurchasePrice = r.PurchasePrice,
            SalesPrice = r.SalesPrice,
            PurchaseAccountCode = r.PurchaseAccountCode?.Trim(),
            SalesAccountCode = r.SalesAccountCode?.Trim(),
            UsefulLifeYears = r.UsefulLifeYears
        };

        _db.Items.Add(item);
        await _db.SaveChangesAsync(ct);

        var category = item.CategoryId.HasValue
            ? await _db.Categories.FindAsync(new object?[] { item.CategoryId.Value }, ct)
            : null;

        return new ItemDetailDto(
            item.Id,
            item.CategoryId,
            category?.Name,
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
