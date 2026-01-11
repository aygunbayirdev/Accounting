using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Services;

public class StockService(IAppDbContext db) : IStockService
{
    public async Task<List<ItemStockDto>> GetStockStatusAsync(List<int> itemIds, CancellationToken ct)
    {
        // 1. Invoices (Giren/Çıkan)
        // Giriş: Alış Faturaları
        // Çıkış: Satış Faturaları
        var invoiceLines = await db.InvoiceLines
            .AsNoTracking()
            .Where(l => l.ItemId.HasValue && itemIds.Contains(l.ItemId.Value))
            .Select(l => new
            {
                l.ItemId,
                Type = l.Invoice.Type,
                l.Qty // InvoiceLine uses Qty (Corrected)
            })
            .ToListAsync(ct);

        // 2. Orders (Rezerve)
        // Kriter: Satış Siparişi + Onaylı (Approved)
        var reservedLines = await db.OrderLines
            .AsNoTracking()
            .Where(l => l.ItemId.HasValue && itemIds.Contains(l.ItemId.Value) &&
                        l.Order.Type == InvoiceType.Sales &&
                        l.Order.Status == OrderStatus.Approved)
            .Select(l => new
            {
                l.ItemId,
                l.Quantity // OrderLine uses Quantity
            })
            .ToListAsync(ct);

        var result = new List<ItemStockDto>();

        foreach (var itemId in itemIds)
        {
            var ins = invoiceLines.Where(x => x.ItemId == itemId && x.Type == InvoiceType.Purchase).Sum(x => x.Qty);
            var outs = invoiceLines.Where(x => x.ItemId == itemId && x.Type == InvoiceType.Sales).Sum(x => x.Qty);
            var reserved = reservedLines.Where(x => x.ItemId == itemId).Sum(x => x.Quantity);

            // Available = (In - Out) - Reserved
            var available = (ins - outs) - reserved;

            result.Add(new ItemStockDto(itemId, ins, outs, reserved, available));
        }

        return result;
    }

    public async Task<ItemStockDto> GetItemStockAsync(int itemId, CancellationToken ct)
    {
        var list = await GetStockStatusAsync(new List<int> { itemId }, ct);
        return list[0];
    }

    public async Task ValidateStockAvailabilityAsync(int itemId, decimal quantityRequired, CancellationToken ct)
    {
        // Hizmet türündeki itemlar stok takibine girmez (Varsayım: Şimdilik tüm Itemlar stoklu kabul ediliyor veya Item entity'sinde type kontrolü yapılmalı)
        // MVP kapsamında her şey stoklu ürün gibi davranıyor, ileride 'Service' flag'i eklenirse buraya 'if (item.IsService) return;' eklenir.

        var stock = await GetItemStockAsync(itemId, ct);

        if (stock.QuantityAvailable < quantityRequired)
        {
            throw new BusinessRuleException($"Stok yetersiz! İstenen: {quantityRequired}, Mevcut (Rezerve Dahil): {stock.QuantityAvailable}, Ürün ID: {itemId}");
        }
    }

    public async Task ValidateBatchStockAvailabilityAsync(Dictionary<int, decimal> stockRequirements, CancellationToken ct)
    {
        if (stockRequirements == null || stockRequirements.Count == 0)
            return;

        var itemIds = stockRequirements.Keys.ToList();
        var stocks = await GetStockStatusAsync(itemIds, ct);

        var insufficientItems = new List<string>();

        foreach (var stock in stocks)
        {
            if (stockRequirements.TryGetValue(stock.ItemId, out var required) && stock.QuantityAvailable < required)
            {
                insufficientItems.Add($"Ürün ID: {stock.ItemId}, İstenen: {required}, Mevcut: {stock.QuantityAvailable}");
            }
        }

        if (insufficientItems.Any())
        {
            throw new BusinessRuleException($"Stok yetersiz! {string.Join("; ", insufficientItems)}");
        }
    }
}
