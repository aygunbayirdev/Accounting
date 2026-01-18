using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Utils;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.StockMovements.Commands.Transfer;

public class TransferStockHandler(IAppDbContext db) : IRequestHandler<TransferStockCommand, StockTransferDetailDto>
{
    public async Task<StockTransferDetailDto> Handle(TransferStockCommand r, CancellationToken ct)
    {
        // 1. Validation & Parsing
        if (r.SourceWarehouseId == r.TargetWarehouseId)
            throw new BusinessRuleException("Kaynak ve hedef depo aynı olamaz.");


        var qty = DecimalExtensions.RoundQuantity(r.Quantity);
        if (qty <= 0)
            throw new BusinessRuleException("Transfer miktarı 0'dan büyük olmalı.");

        // 2. Fetch Warehouses and validate existence
        var sourceWh = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == r.SourceWarehouseId, ct)
             ?? throw new NotFoundException("Source Warehouse", r.SourceWarehouseId);

        var targetWh = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == r.TargetWarehouseId, ct)
             ?? throw new NotFoundException("Target Warehouse", r.TargetWarehouseId);

        // 3. Validation: Branch Consistency
        // Item branch-specific olduğu için, şimdilik sadece aynı şube içi (veya item'ın ait olduğu şube) transferi destekleyelim.
        if (sourceWh.BranchId != targetWh.BranchId)
            throw new BusinessRuleException("Farklı şubeler arası depo transferi (henüz) desteklenmiyor.");
        
        var branchId = sourceWh.BranchId;

        // 4. Validate Item
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == r.ItemId, ct)
            ?? throw new NotFoundException("Item", r.ItemId);

        // 5. Source Stock Check (Snapshot)
        var sourceStock = await db.Stocks.FirstOrDefaultAsync(s => 
            s.WarehouseId == r.SourceWarehouseId && 
            s.ItemId == r.ItemId && 
            s.BranchId == branchId, ct);

        if (sourceStock == null || sourceStock.Quantity < qty)
            throw new BusinessRuleException($"Kaynak depoda yetersiz stok. Mevcut: {(sourceStock?.Quantity ?? 0):N3}, İstenen: {qty:N3}");

        // 6. Target Stock (Snapshot) - Find or Create
        var targetStock = await db.Stocks.FirstOrDefaultAsync(s => 
            s.WarehouseId == r.TargetWarehouseId && 
            s.ItemId == r.ItemId && 
            s.BranchId == branchId, ct);

        if (targetStock == null)
        {
            targetStock = new Stock
            {
                BranchId = branchId,
                WarehouseId = r.TargetWarehouseId,
                ItemId = r.ItemId,
                Quantity = 0m,
                RowVersion = []
            };
            db.Stocks.Add(targetStock);
        }

        // 7. Update Balances
        // Optimistic concurrency (RowVersion) is handled by EF Core automatically if we had concurrency tokens on Read.
        // For Stock, we rely on SaveChanges throwing DbUpdateConcurrencyException if logic demands, 
        // but here we are doing direct modification. 
        // NOTE: In high concurrency, checking `sourceStock.Quantity < qty` in memory then saving might be race condition.
        // Ideally we use database constraints or strict optimistic concurrency.
        // For MVP, standard EF Core handling.

        sourceStock.Quantity = DecimalExtensions.RoundQuantity(sourceStock.Quantity - qty);
        targetStock.Quantity = DecimalExtensions.RoundQuantity(targetStock.Quantity + qty);

        // 8. Create Movements
        var now = r.TransactionDateUtc == default ? DateTime.UtcNow : r.TransactionDateUtc;
        var note = string.IsNullOrWhiteSpace(r.Description) ? "Transfer" : r.Description.Trim();

        var outMovement = new StockMovement
        {
            BranchId = branchId,
            WarehouseId = r.SourceWarehouseId,
            ItemId = r.ItemId,
            Type = StockMovementType.TransferOut,
            Quantity = qty, // Out movements stored as positive quantity in our convention, but logic subtracts.
                            // Wait, CreateStockMovementHandler says: "IsIn ? qty : -qty".
                            // Here we manually adjusted stock. 
                            // But for Reporting consistency, we should store positive Qty.
            TransactionDateUtc = now,
            Note = $"{note} (To: {targetWh.Name})",
            RowVersion = []
        };

        var inMovement = new StockMovement
        {
            BranchId = branchId,
            WarehouseId = r.TargetWarehouseId,
            ItemId = r.ItemId,
            Type = StockMovementType.TransferIn,
            Quantity = qty,
            TransactionDateUtc = now,
            Note = $"{note} (From: {sourceWh.Name})",
            RowVersion = []
        };

        db.StockMovements.Add(outMovement);
        db.StockMovements.Add(inMovement);

        // 9. Save
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("Transfer sırasında stok kaydı değişti. Lütfen tekrar deneyin.");
        }

        return new StockTransferDetailDto(true, outMovement.Id, inMovement.Id, "Transfer başarılı.");
    }
}
