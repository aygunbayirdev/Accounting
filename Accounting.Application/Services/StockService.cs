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
        // 1. Invoices (Giren/Çıkan) - 🔧 SADECE INVENTORY TİPİNDEKİ ITEM'LAR
        var invoiceLines = await db.InvoiceLines
            .AsNoTracking()
            .Include(l => l.Item)  // 🆕 EKLENDI
            .Where(l => l.ItemId.HasValue
                && itemIds.Contains(l.ItemId.Value)
                && l.Item != null
                && l.Item.Type == ItemType.Inventory)  // 🆕 KRİTİK FİLTRE
            .Select(l => new
            {
                l.ItemId,
                l.Invoice.Type,
                l.Qty
            })
            .ToListAsync(ct);

        // 2. Orders (Rezerve) - Zaten sadece stoklu ürünler sipariş edilir
        var reservedLines = await db.OrderLines
            .AsNoTracking()
            .Include(l => l.Item)  // 🆕 EKLENDI
            .Where(l => l.ItemId.HasValue
                && itemIds.Contains(l.ItemId.Value)
                && l.Order.Type == InvoiceType.Sales
                && l.Order.Status == OrderStatus.Approved
                && l.Item != null
                && l.Item.Type == ItemType.Inventory)  // 🆕 EKLENDI
            .Select(l => new
            {
                l.ItemId,
                l.Quantity
            })
            .ToListAsync(ct);

        var result = new List<ItemStockDto>();

        foreach (var itemId in itemIds)
        {
            var ins = invoiceLines.Where(x => x.ItemId == itemId && x.Type == InvoiceType.Purchase).Sum(x => x.Qty);
            var outs = invoiceLines.Where(x => x.ItemId == itemId && x.Type == InvoiceType.Sales).Sum(x => x.Qty);
            var reserved = reservedLines.Where(x => x.ItemId == itemId).Sum(x => x.Quantity);

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
        // 🆕 Item tipini kontrol et - Sadece Inventory için stok kontrolü yap
        var item = await db.Items
            .AsNoTracking()
            .Where(i => i.Id == itemId && !i.IsDeleted)
            .Select(i => new { i.Type })
            .FirstOrDefaultAsync(ct);

        if (item == null)
            throw new NotFoundException("Item", itemId);

        // Service, Expense, FixedAsset için stok kontrolü yapma
        if (item.Type != ItemType.Inventory)
            return;

        var stock = await GetItemStockAsync(itemId, ct);

        if (stock.QuantityAvailable < quantityRequired)
        {
            throw new BusinessRuleException(
                $"Stok yetersiz! İstenen: {quantityRequired}, Mevcut: {stock.QuantityAvailable}, Ürün ID: {itemId}");
        }
    }

    public async Task ValidateBatchStockAvailabilityAsync(Dictionary<int, decimal> stockRequirements, CancellationToken ct)
    {
        if (stockRequirements == null || stockRequirements.Count == 0)
            return;

        var itemIds = stockRequirements.Keys.ToList();

        // 🆕 Önce Item tiplerini kontrol et
        var items = await db.Items
            .AsNoTracking()
            .Where(i => itemIds.Contains(i.Id) && !i.IsDeleted)
            .Select(i => new { i.Id, i.Type })
            .ToListAsync(ct);

        // Sadece Inventory tipindeki item'ları filtrele
        var inventoryItemIds = items
            .Where(i => i.Type == ItemType.Inventory)
            .Select(i => i.Id)
            .ToList();

        if (!inventoryItemIds.Any())
            return;  // Hiç Inventory item yok, stok kontrolü gereksiz

        var stocks = await GetStockStatusAsync(inventoryItemIds, ct);

        var insufficientItems = new List<string>();

        foreach (var stock in stocks)
        {
            if (stockRequirements.TryGetValue(stock.ItemId, out var required)
                && stock.QuantityAvailable < required)
            {
                insufficientItems.Add(
                    $"Ürün ID: {stock.ItemId}, İstenen: {required}, Mevcut: {stock.QuantityAvailable}");
            }
        }

        if (insufficientItems.Any())
        {
            throw new BusinessRuleException($"Stok yetersiz! {string.Join("; ", insufficientItems)}");
        }
    }
}
