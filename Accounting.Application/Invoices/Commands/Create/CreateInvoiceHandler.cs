using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Utils;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
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

    public CreateInvoiceHandler(
        IAppDbContext db,
        IMediator mediator,
        IStockService stockService,
        ICurrentUserService currentUserService,
        IInvoiceNumberService invoiceNumberService)
    {
        _db = db;
        _mediator = mediator;
        _stockService = stockService;
        _currentUserService = currentUserService;
        _invoiceNumberService = invoiceNumberService;
    }

    public async Task<CreateInvoiceResult> Handle(CreateInvoiceCommand req, CancellationToken ct)
    {
        var branchId = _currentUserService.BranchId
            ?? throw new UnauthorizedAccessException("Branch context missing");

        var dateUtc = DateTime.SpecifyKind(req.DateUtc, DateTimeKind.Utc);
        var waybillDate = req.WaybillDateUtc.HasValue
            ? DateTime.SpecifyKind(req.WaybillDateUtc.Value, DateTimeKind.Utc)
            : (DateTime?)null;
        var dueDate = req.PaymentDueDateUtc.HasValue
            ? DateTime.SpecifyKind(req.PaymentDueDateUtc.Value, DateTimeKind.Utc)
            : (DateTime?)null;

        var currency = (req.Currency ?? "TRY").ToUpperInvariant();
        var invType = req.Type;

        var itemIds = req.Lines
            .Where(x => x.ItemId.HasValue)
            .Select(l => l.ItemId!.Value)
            .Distinct()
            .ToList();

        // Stok kontrolü (sadece Sales için)
        if (invType == InvoiceType.Sales)
        {
            var stockRequirements = req.Lines
                .Where(l => l.ItemId.HasValue)
                .GroupBy(l => l.ItemId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(l => l.Qty)
                );

            if (stockRequirements.Any())
            {
                await _stockService.ValidateBatchStockAvailabilityAsync(stockRequirements, ct);
            }
        }

        var itemsMap = await _db.Items
           .Where(i => itemIds.Contains(i.Id))
           .Select(i => new {
               i.Id,
               i.Code,
               i.Name,
               i.Unit,
               type = i.Type,
               i.DefaultWithholdingRate
           })
           .ToDictionaryAsync(i => i.Id, i => (dynamic)i, ct);

        var invoiceNumberPrefix = Accounting.Application.Services.InvoiceNumberService.GetPrefix(invType);
        var invoiceNumber = await _invoiceNumberService.GenerateNextAsync(branchId, invoiceNumberPrefix, ct);

        var invoice = new Invoice
        {
            BranchId = branchId,
            ContactId = req.ContactId,
            DateUtc = dateUtc,
            Currency = currency,
            Type = invType,
            DocumentType = req.DocumentType ?? Domain.Enums.DocumentType.Invoice,
            InvoiceNumber = invoiceNumber,
            WaybillNumber = req.WaybillNumber,
            WaybillDateUtc = waybillDate,
            PaymentDueDateUtc = dueDate,
            CurrencyRate = 1.0m
        };

        decimal totalLineGross = 0;
        decimal totalDiscount = 0;
        decimal totalNet = 0;
        decimal totalVat = 0;
        decimal totalWithholding = 0;

        foreach (var lineDto in req.Lines)
        {
            if (!lineDto.ItemId.HasValue)
                throw new FluentValidation.ValidationException("ItemId is required for all invoice lines");

            var itemId = lineDto.ItemId.Value;
            if (!itemsMap.ContainsKey(itemId))
                throw new NotFoundException("Item", itemId);

            var item = itemsMap[itemId];

            var line = new InvoiceLine
            {
                ItemId = itemId,
                ItemCode = item.Code,
                ItemName = item.Name,
                Unit = item.Unit,
                Qty = lineDto.Qty,
                UnitPrice = lineDto.UnitPrice,
                VatRate = lineDto.VatRate,
                WithholdingRate = lineDto.WithholdingRate ?? item.DefaultWithholdingRate ?? 0
            };

            var gross = DecimalExtensions.RoundAmount(line.Qty * line.UnitPrice);
            line.Gross = gross;

            var discountRate = lineDto.DiscountRate ?? 0;
            line.DiscountRate = discountRate;
            line.DiscountAmount = DecimalExtensions.RoundAmount(gross * (discountRate / 100m));

            var net = gross - line.DiscountAmount;
            line.Net = net;

            var vat = DecimalExtensions.RoundAmount(net * (line.VatRate / 100m));
            line.Vat = vat;

            line.WithholdingAmount = DecimalExtensions.RoundAmount(vat * (line.WithholdingRate / 100m));
            line.GrandTotal = net + vat;

            invoice.Lines.Add(line);

            totalLineGross += gross;
            totalDiscount += line.DiscountAmount;
            totalNet += net;
            totalVat += vat;
            totalWithholding += line.WithholdingAmount;
        }

        invoice.TotalLineGross = totalLineGross;
        invoice.TotalDiscount = totalDiscount;
        invoice.TotalNet = totalNet;
        invoice.TotalVat = totalVat;
        invoice.TotalWithholding = totalWithholding;
        invoice.TotalGross = totalNet + totalVat;
        invoice.Balance = invoice.TotalGross - totalWithholding;

        await using var tx = await _db.BeginTransactionAsync(ct);
        try
        {
            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync(ct);

            // Stok hareketi oluşturma
            await CreateStockMovementsAsync(invoice, itemsMap, ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return new CreateInvoiceResult(
            invoice.Id,
            invoice.TotalNet,
            invoice.TotalVat,
            invoice.TotalGross,
            "AwayFromZero"
        );
    }

    private async Task CreateStockMovementsAsync(
        Invoice invoice,
        Dictionary<int, dynamic> itemsMap,
        CancellationToken ct)
    {
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
        var defaultWarehouse = await _db.Warehouses
            .Where(w => w.BranchId == invoice.BranchId && w.IsDefault && !w.IsDeleted)
            .Select(w => new { w.Id })
            .FirstOrDefaultAsync(ct);

        if (defaultWarehouse == null)
        {
            defaultWarehouse = await _db.Warehouses
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

        // Her satır için stok hareketi oluştur
        foreach (var line in invoice.Lines)
        {
            if (line.ItemId == null) continue;

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
