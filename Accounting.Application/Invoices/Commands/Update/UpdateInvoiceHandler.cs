using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Utils;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Invoices.Queries.Dto;
using Accounting.Application.Services;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Accounting.Application.Common.Interfaces;

public sealed class UpdateInvoiceHandler : IRequestHandler<UpdateInvoiceCommand, InvoiceDetailDto>
{
    private readonly IAppDbContext _ctx;
    private readonly IInvoiceBalanceService _balanceService;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public UpdateInvoiceHandler(
        IAppDbContext ctx,
        IInvoiceBalanceService balanceService,
        IMediator mediator,
        ICurrentUserService currentUserService)
    {
        _ctx = ctx;
        _balanceService = balanceService;
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    public async Task<InvoiceDetailDto> Handle(UpdateInvoiceCommand r, CancellationToken ct)
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
        inv.DateUtc = DateTime.SpecifyKind(r.DateUtc, DateTimeKind.Utc);
        inv.ContactId = r.ContactId;
        inv.Type = r.Type;
        inv.DocumentType = r.DocumentType ?? inv.DocumentType;  // 🆕 EKLENDI

        // 4) Header Fields
        var now = DateTime.UtcNow;
        inv.WaybillNumber = r.WaybillNumber;
        inv.WaybillDateUtc = r.WaybillDateUtc.HasValue
            ? DateTime.SpecifyKind(r.WaybillDateUtc.Value, DateTimeKind.Utc)
            : null;
        inv.PaymentDueDateUtc = r.PaymentDueDateUtc.HasValue
            ? DateTime.SpecifyKind(r.PaymentDueDateUtc.Value, DateTimeKind.Utc)
            : null;

        // Reset Totals
        inv.TotalLineGross = 0;
        inv.TotalDiscount = 0;
        inv.TotalNet = 0;
        inv.TotalVat = 0;
        inv.TotalWithholding = 0;
        inv.TotalGross = 0;

        // ❌ KALDIRILDI: ExpenseDefinition fetch logic
        // Sadece Item'ları çek
        var allItemIds = r.Lines
            .Where(x => x.ItemId.HasValue)
            .Select(x => x.ItemId!.Value)
            .Distinct()
            .ToList();

        var itemsMap = await _ctx.Items
            .Where(i => allItemIds.Contains(i.Id))
            .Select(i => new {
                i.Id,
                i.Code,
                i.Name,
                i.Unit,
                i.VatRate,
                type = i.Type,
                i.DefaultWithholdingRate
            })
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
        foreach (var line in inv.Lines.Where(l => !l.IsDeleted))
        {
            if (incomingById.TryGetValue(line.Id, out var dto))
            {
                ProcessLine(inv, line, dto, itemsMap, now);
            }
        }

        // Yeni ekle
        foreach (var dto in r.Lines.Where(x => x.Id == 0))
        {
            var nl = new InvoiceLine { CreatedAtUtc = now };
            inv.Lines.Add(nl);
            ProcessLine(inv, nl, dto, itemsMap, now);
        }

        // UpdatedAt
        inv.UpdatedAtUtc = now;

        // RE-SUM from active lines
        var activeLines = inv.Lines.Where(l => !l.IsDeleted).ToList();
        inv.TotalLineGross = DecimalExtensions.RoundAmount(activeLines.Sum(x => x.Gross));
        inv.TotalDiscount = DecimalExtensions.RoundAmount(activeLines.Sum(x => x.DiscountAmount));
        inv.TotalNet = DecimalExtensions.RoundAmount(activeLines.Sum(x => x.Net));
        inv.TotalVat = DecimalExtensions.RoundAmount(activeLines.Sum(x => x.Vat));
        inv.TotalWithholding = DecimalExtensions.RoundAmount(activeLines.Sum(x => x.WithholdingAmount));
        inv.TotalGross = DecimalExtensions.RoundAmount(inv.TotalNet + inv.TotalVat);

        // Balance Update
        inv.Balance = inv.TotalGross - inv.TotalWithholding;
        await _balanceService.RecalculateBalanceAsync(inv.Id, ct);

        // Transaction
        await using var tx = await _ctx.BeginTransactionAsync(ct);
        try
        {
            try { await _ctx.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            { throw new ConcurrencyConflictException(); }

            // Stok hareketlerini senkronize et
            await SyncStockMovements(inv, itemsMap, ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        // Fresh read
        var fresh = await _ctx.Invoices
            .AsNoTracking()
            .Include(i => i.Branch)
            .Include(i => i.Contact)
            .Include(i => i.Lines)
            .FirstAsync(i => i.Id == inv.Id, ct);

        // Lines → DTO (❌ ExpenseDefinitionId kaldırıldı)
        var linesDto = fresh.Lines
            .OrderBy(l => l.Id)
            .Select(l => new InvoiceLineDto(
                l.Id,
                l.ItemId,
                // ❌ l.ExpenseDefinitionId,  KALDIRILDI
                l.ItemCode,
                l.ItemName,
                l.Unit,
                l.Qty,
                l.UnitPrice,
                l.VatRate,
                l.DiscountRate,
                l.DiscountAmount,
                l.Net,
                l.Vat,
                l.WithholdingRate,
                l.WithholdingAmount,
                l.Gross,
                l.GrandTotal
            ))
            .ToList();

        // DTO build (🆕 DocumentType eklendi)
        return new InvoiceDetailDto(
            fresh.Id,
            fresh.ContactId,
            fresh.Contact?.Code ?? "",
            fresh.Contact?.Name ?? "",
            fresh.DateUtc,
            fresh.InvoiceNumber,
            fresh.Currency,
            fresh.TotalLineGross,
            fresh.TotalDiscount,
            fresh.TotalNet,
            fresh.TotalVat,
            fresh.TotalWithholding,
            fresh.TotalGross,
            fresh.Balance,
            linesDto,
            (int)fresh.Type,
            (int)fresh.DocumentType,  // 🆕 EKLENDI
            fresh.BranchId,
            fresh.Branch.Code,
            fresh.Branch.Name,
            fresh.WaybillNumber,
            fresh.WaybillDateUtc,
            fresh.PaymentDueDateUtc,
            Convert.ToBase64String(fresh.RowVersion),
            fresh.CreatedAtUtc,
            fresh.UpdatedAtUtc
        );
    }

    private void ProcessLine(
        Invoice inv,
        InvoiceLine line,
        UpdateInvoiceLineDto dto,
        Dictionary<int, dynamic> itemsMap,
        DateTime now)
    {
        // ❌ KALDIRILDI: InvoiceType.Expense kontrolü
        // Artık sadece ItemId var

        if (!dto.ItemId.HasValue)
            throw new FluentValidation.ValidationException("ItemId is required for all invoice lines");

        line.ItemId = dto.ItemId.Value;

        if (!itemsMap.TryGetValue(line.ItemId.Value, out var item))
            throw new NotFoundException("Item", line.ItemId.Value);

        // Snapshot bilgileri
        line.ItemCode = item.Code;
        line.ItemName = item.Name;
        line.Unit = item.Unit;

        decimal discountRate = dto.DiscountRate ?? 0;
        int withholdingRate = dto.WithholdingRate ?? item.DefaultWithholdingRate ?? 0;

        // Calculations
        var gross = DecimalExtensions.RoundQuantity(dto.Qty * dto.UnitPrice);
        var discountAmount = DecimalExtensions.RoundAmount(gross * discountRate / 100m);
        var net = gross - discountAmount;
        var vatAmount = DecimalExtensions.RoundAmount(net * dto.VatRate / 100m);
        var withholdingAmount = DecimalExtensions.RoundAmount(vatAmount * withholdingRate / 100m);
        var grandTotal = net + vatAmount;

        // Assign to Line
        line.Qty = dto.Qty;
        line.UnitPrice = dto.UnitPrice;
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
    }

    private async Task SyncStockMovements(
        Invoice invoice,
        Dictionary<int, dynamic> itemsMap,
        CancellationToken ct)
    {
        // Mevcut hareketleri bul ve sil (Reset)
        var existingMovements = await _ctx.StockMovements
            .Where(m => m.InvoiceId == invoice.Id && !m.IsDeleted)
            .ToListAsync(ct);

        foreach (var move in existingMovements)
        {
            move.IsDeleted = true;
            move.DeletedAtUtc = DateTime.UtcNow;
        }

        // Hareket tipi belirle
        StockMovementType? movementType = invoice.Type switch
        {
            InvoiceType.Sales => StockMovementType.SalesOut,
            InvoiceType.SalesReturn => StockMovementType.SalesReturn,
            InvoiceType.Purchase => StockMovementType.PurchaseIn,
            InvoiceType.PurchaseReturn => StockMovementType.PurchaseReturn,
            _ => null
        };

        if (movementType == null) return;

        // Varsayılan depoyu bul
        var defaultWarehouse = await _ctx.Warehouses
            .Where(w => w.BranchId == invoice.BranchId && w.IsDefault && !w.IsDeleted)
            .Select(w => new { w.Id })
            .FirstOrDefaultAsync(ct);

        if (defaultWarehouse == null)
        {
            defaultWarehouse = await _ctx.Warehouses
                .Where(w => w.BranchId == invoice.BranchId && !w.IsDeleted)
                .OrderBy(w => w.Id)
                .Select(w => new { w.Id })
                .FirstOrDefaultAsync(ct);
        }

        if (defaultWarehouse == null)
        {
            throw new BusinessRuleException(
                $"Şube (BranchId: {invoice.BranchId}) için tanımlı depo bulunamadı.");
        }

        // Yeni hareketler oluştur
        foreach (var line in invoice.Lines)
        {
            if (line.ItemId == null || line.IsDeleted) continue;

            // Sadece Inventory tipindeki item'lar için stok hareketi
            if (itemsMap.TryGetValue(line.ItemId.Value, out var item))
            {
                if ((ItemType)item.type != ItemType.Inventory) continue;
            }

            var absQty = line.Qty;
            if (absQty == 0) continue;

            var cmd = new Accounting.Application.StockMovements.Commands.Create.CreateStockMovementCommand(
                WarehouseId: defaultWarehouse.Id,
                ItemId: line.ItemId.Value,
                Type: movementType.Value,
                Quantity: DecimalExtensions.RoundQuantity(absQty),
                TransactionDateUtc: invoice.DateUtc,
                Note: null,
                InvoiceId: invoice.Id
            );

            await _mediator.Send(cmd, ct);
        }
    }
}
