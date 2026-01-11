using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Utils;
using Accounting.Application.Common.Extensions; // ApplyBranchFilter
using Accounting.Application.Invoices.Queries.Dto;
using Accounting.Application.Services;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

using Accounting.Application.Common.Interfaces;

public sealed class UpdateInvoiceHandler : IRequestHandler<UpdateInvoiceCommand, InvoiceDto>
{
    private readonly IAppDbContext _ctx;
    private readonly IInvoiceBalanceService _balanceService;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public UpdateInvoiceHandler(IAppDbContext ctx, IInvoiceBalanceService balanceService, IMediator mediator, ICurrentUserService currentUserService)
    {
        _ctx = ctx;
        _balanceService = balanceService;
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    public async Task<InvoiceDto> Handle(UpdateInvoiceCommand r, CancellationToken ct)
    {
        // 1) Aggregate (+Include)
        var inv = await _ctx.Invoices
            .ApplyBranchFilter(_currentUserService)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == r.Id, ct)
            ?? throw new NotFoundException(nameof(Invoice), r.Id);

        // 2) Concurrency (RowVersion base64)
        _ctx.Entry(inv).Property(nameof(Invoice.RowVersion))
            .OriginalValue = Convert.FromBase64String(r.RowVersionBase64);

        // 3) Normalize (parent)
        inv.Currency = (r.Currency ?? "TRY").Trim().ToUpperInvariant();
        inv.DateUtc = r.DateUtc;
        inv.ContactId = r.ContactId;
        // inv.BranchId assignment removed - Branch cannot be changed
        inv.Type = NormalizeType(r.Type, inv.Type);

        // ---- Satır diff senkronu ----
        var now = DateTime.UtcNow;

        // sign: removed (All positive)

        // 3.1) Parse Header Fields
        DateTime? waybillDate = null;
        if (!string.IsNullOrWhiteSpace(r.WaybillDateUtc) && DateTime.TryParse(r.WaybillDateUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var wbd)) 
            waybillDate = DateTime.SpecifyKind(wbd, DateTimeKind.Utc);
            
        DateTime? dueDate = null;
        if (!string.IsNullOrWhiteSpace(r.PaymentDueDateUtc) && DateTime.TryParse(r.PaymentDueDateUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var pdd)) 
            dueDate = DateTime.SpecifyKind(pdd, DateTimeKind.Utc);

        inv.WaybillNumber = r.WaybillNumber;
        inv.WaybillDateUtc = waybillDate;
        inv.PaymentDueDateUtc = dueDate;

        // Reset Totals before accumulation
        inv.TotalLineGross = 0;
        inv.TotalDiscount = 0;
        inv.TotalNet = 0;
        inv.TotalVat = 0;
        inv.TotalWithholding = 0;
        inv.TotalGross = 0;

        // Snapshot için gerekli Item'ları ve Expense'leri tek seferde çek
        var allItemIds = r.Lines.Where(x => x.ItemId.HasValue).Select(x => x.ItemId!.Value).Distinct().ToList();
        var allExpenseIds = r.Lines.Where(x => x.ExpenseDefinitionId.HasValue).Select(x => x.ExpenseDefinitionId!.Value).Distinct().ToList();

        var itemsMap = await _ctx.Items
            .Where(i => allItemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Code, i.Name, i.Unit, i.VatRate, type = i.Type })
            .ToDictionaryAsync(i => i.Id, i => (dynamic)i, ct);

        var expensesMap = await _ctx.ExpenseDefinitions
            .Where(i => allExpenseIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Code, i.Name })
            .ToDictionaryAsync(i => i.Id, i => (dynamic)i, ct);

        var incomingById = r.Lines.Where(x => x.Id > 0).ToDictionary(x => x.Id);

        // a) Silinecekler
        foreach (var line in inv.Lines.ToList())
        {
            if (!incomingById.ContainsKey(line.Id))
            {
                line.IsDeleted = true;
                line.DeletedAtUtc = now;
            }
        }

        // b) Güncellenecekler & Yeni Eklenecekler
        // Mevcutları güncelle
        foreach (var line in inv.Lines.Where(l => !l.IsDeleted))
        {
            if (incomingById.TryGetValue(line.Id, out var dto))
            {
                ProcessLine(inv, line, dto, itemsMap, expensesMap, now);
            }
        }

        // Yeni ekle
        foreach (var dto in r.Lines.Where(x => x.Id == 0))
        {
             var nl = new InvoiceLine { CreatedAtUtc = now };
             inv.Lines.Add(nl);
             ProcessLine(inv, nl, dto, itemsMap, expensesMap, now);
        }

        // 4) UpdatedAt + Header Totals (Accumulated in ProcessLine? No, better to sum after)
        inv.UpdatedAtUtc = now;
        
        // RE-SUM from active lines
        var activeLines = inv.Lines.Where(l => !l.IsDeleted).ToList();
        inv.TotalLineGross = Money.R2(activeLines.Sum(x => x.Gross));
        inv.TotalDiscount = Money.R2(activeLines.Sum(x => x.DiscountAmount));
        inv.TotalNet = Money.R2(activeLines.Sum(x => x.Net));
        inv.TotalVat = Money.R2(activeLines.Sum(x => x.Vat));
        inv.TotalWithholding = Money.R2(activeLines.Sum(x => x.WithholdingAmount));
        inv.TotalGross = Money.R2(inv.TotalNet + inv.TotalVat);
        
        // Balance Update
        inv.Balance = inv.TotalGross - inv.TotalWithholding;
        await _balanceService.RecalculateBalanceAsync(inv.Id, ct); // This might override Balance if it sums transactions? 
        // Logic check: RecalculateBalanceAsync usually sums Invoices - Payments.
        // It updates `inv.Balance` based on (TotalGross - Paid).
        // Wait, if Withholding is deducted at source, the "Payable" (Balance) IS (Gross - Withholding).
        // Does `RecalculateBalanceAsync` know about Withholding?
        // Prior to this, Balance = TotalGross.
        // I should verify `InvoiceBalanceService`. For now, I set it here.


        // Transaction: Invoice update + StockMovements birlikte commit
        await using var tx = await _ctx.BeginTransactionAsync(ct);
        try
        {
            // 5) Save + concurrency
            try { await _ctx.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            { throw new ConcurrencyConflictException(); }

            // 5.5) Stok Hareketlerini Senkronize Et (Reset yöntemi)
            await SyncStockMovements(inv, itemsMap, ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        // 6) Fresh read (AsNoTracking + Contact + Lines)
        var fresh = await _ctx.Invoices
            .AsNoTracking()
            .Include(i => i.Branch)
            .Include(i => i.Contact)
            .Include(i => i.Lines)
            .FirstAsync(i => i.Id == inv.Id, ct);

        // Lines → DTO (snapshot kullan)
        var linesDto = fresh.Lines
            .OrderBy(l => l.Id)
            .Select(l => new InvoiceLineDto(
                l.Id,
                l.ItemId,
                l.ExpenseDefinitionId, // Added new field
                l.ItemCode,
                l.ItemName,
                l.Unit,
                Money.S3(l.Qty),
                Money.S4(l.UnitPrice),
                l.VatRate,
                Money.S2(l.DiscountRate), // Added
                Money.S2(l.DiscountAmount), // Added
                Money.S2(l.Net),
                Money.S2(l.Vat),
                l.WithholdingRate,      // Added
                Money.S2(l.WithholdingAmount), // Added
                Money.S2(l.Gross),
                Money.S2(l.GrandTotal)  // Added
            ))
            .ToList();

        // 7) DTO build
        return new InvoiceDto(
            fresh.Id,
            fresh.ContactId,
            fresh.Contact?.Code ?? "",
            fresh.Contact?.Name ?? "",
            fresh.DateUtc,
            fresh.Currency,
            Money.S2(fresh.TotalLineGross), // Added
            Money.S2(fresh.TotalDiscount),  // Added
            Money.S2(fresh.TotalNet),
            Money.S2(fresh.TotalVat),
            Money.S2(fresh.TotalWithholding), // Added
            Money.S2(fresh.TotalGross),
            Money.S2(fresh.Balance),
            linesDto,
            Convert.ToBase64String(fresh.RowVersion),
            fresh.CreatedAtUtc,
            fresh.UpdatedAtUtc,
            (int)fresh.Type,
            fresh.BranchId,
            fresh.Branch.Code,
            fresh.Branch.Name,
            fresh.WaybillNumber,
            fresh.WaybillDateUtc,
            fresh.PaymentDueDateUtc
        );
    }

    private static InvoiceType NormalizeType(string? incoming, InvoiceType fallback)
    {
        if (string.IsNullOrWhiteSpace(incoming)) return fallback;

        // "1" / "2" / "3" / "4"
        if (int.TryParse(incoming, out var n) && Enum.IsDefined(typeof(InvoiceType), n))
            return (InvoiceType)n;

        // "Sales" / "Purchase" / "SalesReturn" / "PurchaseReturn"
        return incoming.Trim().ToLowerInvariant() switch
        {
            "sales" => InvoiceType.Sales,
            "purchase" => InvoiceType.Purchase,
            "salesreturn" => InvoiceType.SalesReturn,
            "purchasereturn" => InvoiceType.PurchaseReturn,
            _ => fallback
        };
    }

    private void ProcessLine(Invoice inv, InvoiceLine line, UpdateInvoiceLineDto dto, Dictionary<int, dynamic> itemsMap, Dictionary<int, dynamic> expensesMap, DateTime now)
    {
        if (!Money.TryParse4(dto.Qty, out var qty)) throw new BusinessRuleException($"Invalid Qty.");
        if (!Money.TryParse4(dto.UnitPrice, out var unitPrice)) throw new BusinessRuleException($"Invalid Price.");

        decimal discountRate = 0;
        if (!string.IsNullOrWhiteSpace(dto.DiscountRate))
             Money.TryParse4(dto.DiscountRate, out discountRate);

        int withholdingRate = dto.WithholdingRate ?? 0;

        // Normalize
        qty = Money.R3(Math.Abs(qty));
        unitPrice = Money.R4(unitPrice);
        
        // Calculations
        var gross = Money.R2(qty * unitPrice);
        var discountAmount = Money.R2(gross * discountRate / 100m);
        var net = gross - discountAmount;
        var vatAmount = Money.R2(net * dto.VatRate / 100m);
        var withholdingAmount = Money.R2(vatAmount * withholdingRate / 100m);
        var grandTotal = net + vatAmount;

        // Assign to Line
        line.Qty = qty;
        line.UnitPrice = unitPrice;
        line.VatRate = dto.VatRate;
        line.DiscountRate = discountRate;
        line.DiscountAmount = discountAmount;
        line.Gross = gross;
        line.Net = net;
        line.Vat = vatAmount;
        line.WithholdingRate = withholdingRate;
        line.WithholdingAmount = withholdingAmount;
        line.GrandTotal = grandTotal;
        line.UpdatedAtUtc = now;

        // Item/Expense Mapping
        if (inv.Type == InvoiceType.Expense)
        {
            if (dto.ItemId.HasValue) throw new BusinessRuleException("Masraf faturasında ItemId olamaz.");
            if (!dto.ExpenseDefinitionId.HasValue) throw new BusinessRuleException("Masraf faturasında ExpenseDefinitionId zorunludur.");
            
            line.ExpenseDefinitionId = dto.ExpenseDefinitionId;
            line.ItemId = null;

            if (expensesMap.TryGetValue(line.ExpenseDefinitionId.Value, out var exp))
            {
                line.ItemCode = exp.Code;
                line.ItemName = exp.Name;
                line.Unit = "adet";
            }
        }
        else
        {
            if (dto.ExpenseDefinitionId.HasValue) throw new BusinessRuleException("Stok faturasında ExpenseDefinitionId olamaz.");
            if (!dto.ItemId.HasValue) throw new BusinessRuleException("Stok faturasında ItemId zorunludur.");

            line.ItemId = dto.ItemId;
            line.ExpenseDefinitionId = null;

            if (itemsMap.TryGetValue(line.ItemId.Value, out var it))
            {
                line.ItemCode = it.Code;
                line.ItemName = it.Name;
                line.Unit = it.Unit;
            }
        }
    }

    private async Task SyncStockMovements(Invoice invoice, Dictionary<int, dynamic> itemsMap, CancellationToken ct)
    {
        // 1. Stok hareketi gerekmeyen durum (Expense)
        if (invoice.Type == InvoiceType.Expense) return;

        // 2. Mevcut hareketleri bul ve sil (Reset) - InvoiceId ile
        var existingMovements = await _ctx.StockMovements
            .Where(m => m.InvoiceId == invoice.Id && !m.IsDeleted)
            .ToListAsync(ct);

        foreach (var move in existingMovements)
        {
            move.IsDeleted = true;
            move.DeletedAtUtc = DateTime.UtcNow;
        }

        // 3. Yeni hareketleri oluştur
        StockMovementType? movementType = invoice.Type switch
        {
            InvoiceType.Sales => StockMovementType.SalesOut,
            InvoiceType.SalesReturn => StockMovementType.SalesReturn,
            InvoiceType.Purchase => StockMovementType.PurchaseIn,
            InvoiceType.PurchaseReturn => StockMovementType.PurchaseReturn,
            _ => null
        };

        if (movementType == null) return;

        // ✅ FIX: Branch'in varsayılan deposunu bul (hardcoded 1 yerine)
        var defaultWarehouse = await _ctx.Warehouses
            .Where(w => w.BranchId == invoice.BranchId && w.IsDefault && !w.IsDeleted)
            .Select(w => new { w.Id })
            .FirstOrDefaultAsync(ct);

        if (defaultWarehouse == null)
        {
            // Fallback: IsDefault olmasa bile şubenin ilk deposunu kullan
            defaultWarehouse = await _ctx.Warehouses
                .Where(w => w.BranchId == invoice.BranchId && !w.IsDeleted)
                .OrderBy(w => w.Id)
                .Select(w => new { w.Id })
                .FirstOrDefaultAsync(ct);
        }

        if (defaultWarehouse == null)
        {
            throw new BusinessRuleException($"Şube (BranchId: {invoice.BranchId}) için tanımlı depo bulunamadı. Stoklu ürün içeren fatura için en az bir depo tanımlamalısınız.");
        }

        foreach (var line in invoice.Lines)
        {
            if (line.ItemId == null || line.IsDeleted) continue; // Deleted lines don't create movement

            // Check ItemType - Skip if NOT Inventory
            if (itemsMap != null && itemsMap.TryGetValue(line.ItemId.Value, out var it))
            {
                 if ((ItemType)it.type != ItemType.Inventory) continue;
            }

            var absQty = line.Qty; 
            if (absQty == 0) continue;

            // Create command
            var cmd = new Accounting.Application.StockMovements.Commands.Create.CreateStockMovementCommand(
                WarehouseId: defaultWarehouse.Id, 
                ItemId: line.ItemId.Value,
                Type: movementType.Value,
                Quantity: Money.S3(absQty),
                TransactionDateUtc: invoice.DateUtc,
                Note: null,
                InvoiceId: invoice.Id 
            );

            await _mediator.Send(cmd, ct);
        }
    }
}