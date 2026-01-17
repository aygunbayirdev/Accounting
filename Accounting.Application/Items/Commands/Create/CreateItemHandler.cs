using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Utils;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Items.Queries.Dto;
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

using Accounting.Application.Common.Interfaces;

namespace Accounting.Application.Items.Commands.Create;

public class CreateItemHandler : IRequestHandler<CreateItemCommand, ItemDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    public CreateItemHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<ItemDetailDto> Handle(CreateItemCommand r, CancellationToken ct)
    {
        decimal? pPrice = null;
        if (r.PurchasePrice is not null)
        {
            Money.TryParse2(r.PurchasePrice, out var pp);
            pPrice = Money.R2(pp);
        }

        decimal? sPrice = null;
        if (r.SalesPrice is not null)
        {
            Money.TryParse2(r.SalesPrice, out var sp);
            sPrice = Money.R2(sp);
        }

        var branchId = _currentUserService.BranchId ?? throw new UnauthorizedAccessException();

        // Check for duplicate code
        var exists = await _db.Items.AnyAsync(x => x.BranchId == branchId && x.Code == r.Code.Trim(), ct);
        if (exists) throw new BusinessRuleException($"Bu kod ({r.Code}) ile kayıtlı stok/hizmet zaten var.");

        var e = new Item
        {
            BranchId = branchId,
            CategoryId = r.CategoryId,
            Code = r.Code.Trim(),
            Name = r.Name.Trim(),
            Type = (Domain.Enums.ItemType)r.Type,
            Unit = r.Unit.Trim(),
            VatRate = r.VatRate,
            DefaultWithholdingRate = r.DefaultWithholdingRate,
            PurchasePrice = pPrice,
            SalesPrice = sPrice
            // Created/Updated defaults via interceptor
        };

        _db.Items.Add(e);
        await _db.SaveChangesAsync(ct);

        // Fresh read
        var saved = await _db.Items.AsNoTracking().FirstAsync(x => x.Id == e.Id, ct);

        // Kategori ismi look up
        string? catName = null;
        if (e.CategoryId.HasValue)
        {
            var cat = await _db.Categories.FindAsync(new object[] { e.CategoryId.Value }, ct);
            catName = cat?.Name;
        }

        return new ItemDetailDto(
            saved.Id,
            saved.CategoryId,
            catName,
            saved.Code,
            saved.Name,
            (int)saved.Type,
            saved.Unit,
            saved.VatRate,
            saved.DefaultWithholdingRate ?? 0,
            saved.PurchasePrice is null ? null : Money.S2(saved.PurchasePrice.Value),
            saved.SalesPrice is null ? null : Money.S2(saved.SalesPrice.Value),
            Convert.ToBase64String(saved.RowVersion),
            saved.CreatedAtUtc,
            saved.UpdatedAtUtc
        );
    }
}
