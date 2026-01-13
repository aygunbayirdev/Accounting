using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Utils;                 // Money helper
using Accounting.Domain.Entities;                          // Invoice, InvoiceLine, InvoiceType
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

using Accounting.Application.Common.Interfaces;

namespace Accounting.Application.Invoices.Commands.Create;

public class CreateInvoiceHandler
    : IRequestHandler<CreateInvoiceCommand, CreateInvoiceResult>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IStockService _stockService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInvoiceNumberService _invoiceNumberService;

    public CreateInvoiceHandler(IAppDbContext db, IMediator mediator, IStockService stockService, ICurrentUserService currentUserService, IInvoiceNumberService invoiceNumberService)
    {
        _db = db;
        _mediator = mediator;
        _stockService = stockService;
        _currentUserService = currentUserService;
        _invoiceNumberService = invoiceNumberService;
    }

    public async Task<CreateInvoiceResult> Handle(CreateInvoiceCommand req, CancellationToken ct)
    {
        var branchId = _currentUserService.BranchId ?? throw new UnauthorizedAccessException("Branch context missing");

        // 1) Parse DateUtc
        if (!DateTime.TryParse(req.DateUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dateUtc))
            throw new ArgumentException("DateUtc is invalid.");
        dateUtc = DateTime.SpecifyKind(dateUtc, DateTimeKind.Utc);

        // 1.1) Parse Waybill Date & Due Date
        DateTime? waybillDate = null;
        if (!string.IsNullOrWhiteSpace(req.WaybillDateUtc))
        {
            if (DateTime.TryParse(req.WaybillDateUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var wbDate))
                waybillDate = DateTime.SpecifyKind(wbDate, DateTimeKind.Utc);
        }

        DateTime? dueDate = null;
        if (!string.IsNullOrWhiteSpace(req.PaymentDueDateUtc))
        {
            if (DateTime.TryParse(req.PaymentDueDateUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var ddDate))
                dueDate = DateTime.SpecifyKind(ddDate, DateTimeKind.Utc);
        }

        // 2) Normalize
        var currency = (req.Currency ?? "TRY").ToUpperInvariant();
        var invType = NormalizeType(req.Type, InvoiceType.Sales);

        // 3) Validate Items/Expenses (Fetch Map)
        Dictionary<int, dynamic>? itemsMap = null;
        Dictionary<int, dynamic>? expensesMap = null;

        if (invType == InvoiceType.Expense)
        {
            var expenseIds = req.Lines.Where(x => x.ExpenseDefinitionId.HasValue).Select(l => l.ExpenseDefinitionId!.Value).Distinct().ToList();
            expensesMap = await _db.ExpenseDefinitions
               .Where(i => expenseIds.Contains(i.Id))
               .Select(i => new { i.Id, i.Code, i.Name })
               .ToDictionaryAsync(i => i.Id, i => (dynamic)i, ct);
        }
        else
        {
            var itemIds = req.Lines.Where(x => x.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
            
            // Stock Validation (Batch - Performance optimized)
            if (invType == InvoiceType.Sales)
            {
                var stockRequirements = req.Lines
                    .Where(l => l.ItemId.HasValue && decimal.TryParse(l.Qty, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                    .GroupBy(l => l.ItemId!.Value)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(l => decimal.TryParse(l.Qty, NumberStyles.Number, CultureInfo.InvariantCulture, out var q) ? Math.Abs(q) : 0m)
                    );

                if (stockRequirements.Any())
                {
                    await _stockService.ValidateBatchStockAvailabilityAsync(stockRequirements, ct);
                }
            }

            itemsMap = await _db.Items
               .Where(i => itemIds.Contains(i.Id))
               .Select(i => new { i.Id, i.Code, i.Name, i.Unit, type = i.Type, i.DefaultWithholdingRate })
               .ToDictionaryAsync(i => i.Id, i => (dynamic)i, ct);
        }

        // 4) Generate Invoice Number
        var invoiceNumberPrefix = Accounting.Application.Services.InvoiceNumberService.GetPrefix(invType);
        var invoiceNumber = await _invoiceNumberService.GenerateNextAsync(branchId, invoiceNumberPrefix, ct);

        // 5) Initialize Invoice
        var invoice = new Invoice
        {
            BranchId = branchId,
            ContactId = req.ContactId,
            DateUtc = dateUtc,
            Currency = currency,
            Type = invType,
            InvoiceNumber = invoiceNumber,
            WaybillNumber = req.WaybillNumber,
            WaybillDateUtc = waybillDate,
            PaymentDueDateUtc = dueDate,
            Lines = new List<InvoiceLine>()
        };

        // 5) Process Lines
        foreach (var lineDto in req.Lines)
        {
            // Parse Decimals
            if (!decimal.TryParse(lineDto.Qty, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty)) throw new ArgumentException("Qty invalid");
            if (!decimal.TryParse(lineDto.UnitPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var unitPrice)) throw new ArgumentException("Price invalid");
            
            decimal discountRate = 0;
            if (!string.IsNullOrWhiteSpace(lineDto.DiscountRate))
                decimal.TryParse(lineDto.DiscountRate, NumberStyles.Number, CultureInfo.InvariantCulture, out discountRate);

            int withholdingRate = lineDto.WithholdingRate ?? 0;

            // Normalize
            qty = Money.R3(Math.Abs(qty)); // Always positive stored
            unitPrice = Money.R4(unitPrice);
            
            // -- CALCULATIONS --
            // 1. Gross (Brüt) = Qty * Price
            var gross = Money.R2(qty * unitPrice);

            // 2. Discount Amount
            var discountAmount = Money.R2(gross * discountRate / 100m);

            // 3. Net (Matrah)
            var net = gross - discountAmount;

            // 4. VAT
            var vatAmount = Money.R2(net * lineDto.VatRate / 100m);

            // 5. Withholding
            var withholdingAmount = Money.R2(vatAmount * withholdingRate / 100m);

            // 6. Grand Total (Line) -> Net + Vat
            var lineGrandTotal = net + vatAmount; 

            // Create Entity
            var lineEntity = new InvoiceLine
            {
                Qty = qty,
                UnitPrice = unitPrice,
                VatRate = lineDto.VatRate,
                Gross = gross,
                DiscountRate = discountRate,
                DiscountAmount = discountAmount,
                Net = net,
                Vat = vatAmount,
                WithholdingRate = withholdingRate,
                WithholdingAmount = withholdingAmount,
                GrandTotal = lineGrandTotal
            };

            // Map Details
            if (invType == InvoiceType.Expense)
            {
                 if (!lineDto.ExpenseDefinitionId.HasValue) throw new BusinessRuleException("ExpenseDefinitionId required");
                 if (expensesMap == null || !expensesMap.TryGetValue(lineDto.ExpenseDefinitionId.Value, out var exp)) throw new BusinessRuleException("Expense not found");
                 lineEntity.ExpenseDefinitionId = lineDto.ExpenseDefinitionId;
                 lineEntity.ItemCode = exp.Code;
                 lineEntity.ItemName = exp.Name;
                 lineEntity.Unit = "adet";
            }
            else
            {
                 if (!lineDto.ItemId.HasValue) throw new BusinessRuleException("ItemId required");
                 if (itemsMap == null || !itemsMap.TryGetValue(lineDto.ItemId.Value, out var it)) throw new BusinessRuleException("Item not found");
                 lineEntity.ItemId = lineDto.ItemId;
                 lineEntity.ItemCode = it.Code;
                 lineEntity.ItemName = it.Name;
                 lineEntity.Unit = it.Unit;
            }

            invoice.Lines.Add(lineEntity);

            // Add to Header Totals
            invoice.TotalLineGross += lineEntity.Gross;
            invoice.TotalDiscount += lineEntity.DiscountAmount;
            invoice.TotalNet += lineEntity.Net;
            invoice.TotalVat += lineEntity.Vat;
            invoice.TotalWithholding += lineEntity.WithholdingAmount;
        }

        // Finalize Header
        invoice.TotalGross = invoice.TotalNet + invoice.TotalVat; // Genel Toplam
        // Balance (Kalan Ödenecek/Alacak) = Genel Toplam - Tevkifat (Tevkifatı devlet öder/biz öderiz)
        invoice.Balance = invoice.TotalGross - invoice.TotalWithholding;

        // DB Save
        await using var tx = await _db.BeginTransactionAsync(ct);
        try
        {
            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync(ct);
            await CreateStockMovements(invoice, itemsMap, ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return new CreateInvoiceResult(
            Id: invoice.Id,
            TotalNet: Money.S2(invoice.TotalNet),
            TotalVat: Money.S2(invoice.TotalVat),
            TotalGross: Money.S2(invoice.TotalGross),
            RoundingPolicy: "AwayFromZero"
        );
    }

    private async Task CreateStockMovements(Invoice invoice, Dictionary<int, dynamic>? itemsMap, CancellationToken ct)
    {
        // Fatura tipine göre Stok hareket yönünü belirle
        // Sales -> SalesOut (Çıkış)
        // SalesReturn -> SalesReturn (Giriş)
        // Purchase -> PurchaseIn (Giriş)
        // PurchaseReturn -> PurchaseReturn (Çıkış)

        StockMovementType? movementType = invoice.Type switch
        {
            InvoiceType.Sales => StockMovementType.SalesOut,
            InvoiceType.SalesReturn => StockMovementType.SalesReturn,
            InvoiceType.Purchase => StockMovementType.PurchaseIn,
            InvoiceType.PurchaseReturn => StockMovementType.PurchaseReturn,
            _ => null
        };

        if (movementType == null) return; // Proforma vb. ise hareket yok

        // Expense (Masraf) Faturası ise stok hareketi OLUŞTURMA
        if (invoice.Type == InvoiceType.Expense) return;

        // ✅ FIX: Branch'in varsayılan deposunu bul (hardcoded 1 yerine)
        var defaultWarehouse = await _db.Warehouses
            .Where(w => w.BranchId == invoice.BranchId && w.IsDefault && !w.IsDeleted)
            .Select(w => new { w.Id })
            .FirstOrDefaultAsync(ct);

        if (defaultWarehouse == null)
        {
            // Fallback: IsDefault olmasa bile şubenin ilk deposunu kullan
            defaultWarehouse = await _db.Warehouses
                .Where(w => w.BranchId == invoice.BranchId && !w.IsDeleted)
                .OrderBy(w => w.Id)
                .Select(w => new { w.Id })
                .FirstOrDefaultAsync(ct);
        }

        if (defaultWarehouse == null)
        {
            // Şubenin deposu yoksa stoklu ürün faturalandırılamaz
            throw new BusinessRuleException($"Şube (BranchId: {invoice.BranchId}) için tanımlı depo bulunamadı. Stoklu ürün içeren fatura oluşturmadan önce en az bir depo tanımlamalısınız.");
        }

        foreach (var line in invoice.Lines)
        {
            // Eğer yanlışlıkla satıra Item koyulmadıysa devam et (Validasyon zaten var ama defensive coding)
            if (line.ItemId == null) continue;

            // Check ItemType - Skip if NOT Inventory
            if (itemsMap != null && itemsMap.TryGetValue(line.ItemId.Value, out var it))
            {
                 if ((ItemType)it.type != ItemType.Inventory) continue;
            }

            // Qty işareti: InvoiceLine.Qty'de iadelerde negatif tutuyorduk (finansal).
            // Stok servisi "mutlak değer" bekliyor olabilir, ama CreateStockMovementHandler:
            // "IsIn" ise +qty, değilse -qty yapıyor.
            // BİZİM BURADA GÖNDERECEĞİMİZ "Miktar" HER ZAMAN POZİTİF OLMALI.
            // CreateStockMovementHandler kendi içinde Type'a göre artırıp azaltacak.

            var absQty = Math.Abs(line.Qty);
            if (absQty == 0) continue;

            var cmd = new Accounting.Application.StockMovements.Commands.Create.CreateStockMovementCommand(
                WarehouseId: defaultWarehouse.Id, // ✅ Dinamik warehouse
                ItemId: line.ItemId!.Value,
                Type: movementType.Value,
                Quantity: Money.S3(absQty), // String format
                TransactionDateUtc: invoice.DateUtc,
                Note: null,
                InvoiceId: invoice.Id // FK ile ilişkilendirme
            );

            await _mediator.Send(cmd, ct);
        }
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
            "expense" => InvoiceType.Expense,
            _ => fallback
        };
    }
}
