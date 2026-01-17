using Accounting.Application.Common.Abstractions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.StockMovements.Commands.Transfer;

public class TransferStockValidator : AbstractValidator<TransferStockCommand>
{
    private readonly IAppDbContext _db;

    public TransferStockValidator(IAppDbContext db)
    {
        _db = db;

        RuleFor(x => x.SourceWarehouseId)
            .GreaterThan(0)
            .MustAsync(WarehouseExistsAsync).WithMessage("Kaynak depo bulunamadı.")
            .NotEqual(x => x.TargetWarehouseId).WithMessage("Kaynak ve hedef depo aynı olamaz.");

        RuleFor(x => x.TargetWarehouseId)
            .GreaterThan(0)
            .MustAsync(WarehouseExistsAsync).WithMessage("Hedef depo bulunamadı.");

        RuleFor(x => x.ItemId)
            .GreaterThan(0)
            .MustAsync(ItemExistsAsync).WithMessage("Ürün bulunamadı.");    

        RuleFor(x => x.TransactionDateUtc)
            .NotEmpty();

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }

    private async Task<bool> WarehouseExistsAsync(int warehouseId, CancellationToken ct)
    {
        return await _db.Warehouses.AnyAsync(w => w.Id == warehouseId, ct);
    }

    private async Task<bool> ItemExistsAsync(int itemId, CancellationToken ct)
    {
        return await _db.Items.AnyAsync(i => i.Id == itemId, ct);
    }
}
