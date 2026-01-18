using Accounting.Application.Common.Abstractions;
using Accounting.Application.Reports.Queries.Dtos;
using Accounting.Application.Reports.Queries.GetIncomeExpense;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Reports.Queries.GetProfitLoss;

/// <summary>
/// Gelir-Gider Raporu (Income & Expense Report)
/// ⚠️ NAKİT BAZLI: Gerçek muhasebe karı değildir.
/// Stok alımları COGS (Satılan Malın Maliyeti) olarak değil,
/// dönem içi stok harcaması olarak gösterilir.
/// </summary>
public class GetIncomeExpenseHandler(IAppDbContext db)
    : IRequestHandler<GetIncomeExpenseQuery, IncomeExpenseDto>
{
    public async Task<IncomeExpenseDto> Handle(GetIncomeExpenseQuery request, CancellationToken ct)
    {
        var dateFrom = request.DateFrom ?? DateTime.MinValue;
        var dateTo = request.DateTo ?? DateTime.MaxValue;

        // =================================================================
        // 1. NET SATIŞLAR (Sales - Sales Returns)
        // =================================================================
        var salesQuery = db.Invoices
            .AsNoTracking()
            .Where(i => (i.Type == InvoiceType.Sales
                        || i.Type == InvoiceType.SalesReturn)
                && i.DateUtc >= dateFrom
                && i.DateUtc <= dateTo
                && !i.IsDeleted);

        if (request.BranchId.HasValue)
            salesQuery = salesQuery.Where(i => i.BranchId == request.BranchId.Value);

        var salesData = await salesQuery
            .Select(i => new { i.Type, i.TotalNet, i.TotalVat })
            .ToListAsync(ct);

        var totalSales = salesData
            .Where(i => i.Type == InvoiceType.Sales)
            .Sum(s => s.TotalNet);

        var totalSalesReturns = salesData
            .Where(i => i.Type == InvoiceType.SalesReturn)
            .Sum(s => s.TotalNet);

        var income = totalSales - totalSalesReturns;

        var salesVat = salesData
            .Where(i => i.Type == InvoiceType.Sales)
            .Sum(s => s.TotalVat);

        var salesReturnVat = salesData
            .Where(i => i.Type == InvoiceType.SalesReturn)
            .Sum(s => s.TotalVat);

        var netOutputVat = salesVat - salesReturnVat;

        // =================================================================
        // 2. STOK ALIMLARI (Inventory Purchases - NOT Real COGS!)
        // ⚠️ Bu değer "Satılan Malın Maliyeti" değil, "Dönem İçi Stok
        //    Alımları"dır. Gerçek COGS için satış anında maliyet hesabı
        //    gerekir (FIFO/LIFO).
        // =================================================================
        var inventoryLinesQuery = db.InvoiceLines
            .AsNoTracking()
            .Include(l => l.Invoice)
            .Include(l => l.Item)
            .Where(l => !l.IsDeleted
                && (l.Invoice.Type == InvoiceType.Purchase
                    || l.Invoice.Type == InvoiceType.PurchaseReturn)
                && l.Invoice.DateUtc >= dateFrom
                && l.Invoice.DateUtc <= dateTo
                && !l.Invoice.IsDeleted
                && l.Item != null
                && l.Item.Type == ItemType.Inventory);

        if (request.BranchId.HasValue)
            inventoryLinesQuery = inventoryLinesQuery
                .Where(l => l.Invoice.BranchId == request.BranchId.Value);

        var inventoryLines = await inventoryLinesQuery
            .Select(l => new { l.Invoice.Type, l.Net, l.Vat })
            .ToListAsync(ct);

        var inventoryPurchases = inventoryLines
            .Where(l => l.Type == InvoiceType.Purchase)
            .Sum(l => l.Net);

        var inventoryPurchaseReturns = inventoryLines
            .Where(l => l.Type == InvoiceType.PurchaseReturn)
            .Sum(l => l.Net);

        var netInventoryPurchases = inventoryPurchases - inventoryPurchaseReturns;

        var inventoryPurchaseVat = inventoryLines
            .Where(l => l.Type == InvoiceType.Purchase)
            .Sum(l => l.Vat);

        var inventoryPurchaseReturnVat = inventoryLines
            .Where(l => l.Type == InvoiceType.PurchaseReturn)
            .Sum(l => l.Vat);

        var netInventoryVat = inventoryPurchaseVat - inventoryPurchaseReturnVat;

        // =================================================================
        // 3. FAALİYET GİDERLERİ (Operating Expenses)
        // Expense (Kira, Elektrik, vb.) + Service (Kargo, Danışmanlık, vb.)
        // =================================================================
        var expenseLinesQuery = db.InvoiceLines
            .AsNoTracking()
            .Include(l => l.Invoice)
            .Include(l => l.Item)
            .Where(l => !l.IsDeleted
                && (l.Invoice.Type == InvoiceType.Purchase
                    || l.Invoice.Type == InvoiceType.PurchaseReturn)
                && l.Invoice.DateUtc >= dateFrom
                && l.Invoice.DateUtc <= dateTo
                && !l.Invoice.IsDeleted
                && l.Item != null
                && (l.Item.Type == ItemType.Expense
                    || l.Item.Type == ItemType.Service));

        if (request.BranchId.HasValue)
            expenseLinesQuery = expenseLinesQuery
                .Where(l => l.Invoice.BranchId == request.BranchId.Value);

        var expenseLines = await expenseLinesQuery
            .Select(l => new { l.Invoice.Type, l.Net, l.Vat })
            .ToListAsync(ct);

        var expensePurchases = expenseLines
            .Where(l => l.Type == InvoiceType.Purchase)
            .Sum(l => l.Net);

        var expensePurchaseReturns = expenseLines
            .Where(l => l.Type == InvoiceType.PurchaseReturn)
            .Sum(l => l.Net);

        var totalExpenses = expensePurchases - expensePurchaseReturns;

        var expensePurchaseVat = expenseLines
            .Where(l => l.Type == InvoiceType.Purchase)
            .Sum(l => l.Vat);

        var expensePurchaseReturnVat = expenseLines
            .Where(l => l.Type == InvoiceType.PurchaseReturn)
            .Sum(l => l.Vat);

        var netExpenseVat = expensePurchaseVat - expensePurchaseReturnVat;

        // =================================================================
        // 4. TOPLAMLAR (Totals)
        // =================================================================

        // Brüt Kâr (NAKİT BAZLI - Gerçek muhasebe karı değildir)
        // Gross Profit = Income - Inventory Purchases
        var grossProfit = income - netInventoryPurchases;

        // Net Kâr/Zarar
        // Net Profit = Gross Profit - Operating Expenses
        var netProfit = grossProfit - totalExpenses;

        // KDV Dengesi (Ödenecek veya İade Alınacak)
        var totalVat = netOutputVat - (netInventoryVat + netExpenseVat);

        return new IncomeExpenseDto(
            income,
            netInventoryPurchases,  // "COGS" olarak GÖSTERİLMEMELİ - "Inventory Purchases"
            totalExpenses,
            grossProfit,
            netProfit,
            totalVat
        );
    }
}
