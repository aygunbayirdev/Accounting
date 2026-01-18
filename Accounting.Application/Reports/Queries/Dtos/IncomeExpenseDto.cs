namespace Accounting.Application.Reports.Queries.Dtos;

/// <summary>
/// Gelir-Gider Raporu DTO (Income & Expense Report)
/// NAKÝT BAZLI RAPOR - Gerçek muhasebe karý deðildir
/// </summary>
public record IncomeExpenseDto(
    /// <summary>
    /// Net Satýþlar (Sales - Sales Returns)
    /// </summary>
    decimal Income,

    /// <summary>
    /// Stok Alýmlarý (Inventory Purchases)
    /// DÝKKAT: Bu COGS (Satýlan Malýn Maliyeti) DEÐÝLDÝR!
    /// Dönem içinde satýn alýnan mal bedelidir.
    /// Gerçek COGS için FIFO/LIFO sistemi gerekir.
    /// </summary>
    decimal InventoryPurchases,

    /// <summary>
    /// Faaliyet Giderleri (Operating Expenses)
    /// Expense + Service item alýmlarý
    /// </summary>
    decimal OperatingExpenses,

    /// <summary>
    /// Brüt Kâr (Gross Profit)
    /// NAKÝT BAZLI: Income - Inventory Purchases
    /// </summary>
    decimal GrossProfit,

    /// <summary>
    /// Net Kâr/Zarar (Net Profit/Loss)
    /// Gross Profit - Operating Expenses
    /// </summary>
    decimal NetProfit,

    /// <summary>
    /// KDV Dengesi (VAT Balance)
    /// Pozitif: Ödenecek KDV
    /// Negatif: Ýade alýnacak KDV
    /// </summary>
    decimal VatBalance
);
