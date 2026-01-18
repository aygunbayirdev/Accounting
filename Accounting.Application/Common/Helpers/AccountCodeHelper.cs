using Accounting.Domain.Enums;

namespace Accounting.Application.Common.Helpers;

/// <summary>
/// Tek Düzen Hesap Planı (TDHP) kodlarını yöneten helper sınıf.
/// Fatura türü ve item türüne göre doğru muhasebe kodunu döndürür.
/// </summary>
public static class AccountCodeHelper
{
    // ==========================================
    // TEK DÜZEN HESAP PLANI KODLARI
    // ==========================================

    // VARLIK HESAPLARI (Aktif)
    private const string INVENTORY_PURCHASE = "153";      // Ticari Mallar
    private const string FIXED_ASSET_PURCHASE = "255";    // Demirbaşlar

    // MALİYET/GİDER HESAPLARI
    private const string EXPENSE_PURCHASE = "770";        // Genel Üretim Giderleri

    // GELİR HESAPLARI
    private const string INVENTORY_SALES = "600";         // Yurt İçi Satışlar
    private const string SERVICE_SALES = "602";           // Hizmet Satışları

    /// <summary>
    /// Fatura türü ve item türüne göre uygun muhasebe kodunu döndürür.
    /// </summary>
    /// <param name="invoiceType">Fatura tipi (Sales, Purchase, vb.)</param>
    /// <param name="itemType">Item tipi (Inventory, Service, Expense, FixedAsset)</param>
    /// <returns>TDHP muhasebe kodu veya null (hatalı kombinasyon)</returns>
    public static string? GetAccountCode(InvoiceType invoiceType, ItemType itemType)
    {
        return (invoiceType, itemType) switch
        {
            // ====================================
            // SATIŞ FATURALARI (Sales)
            // ====================================
            (InvoiceType.Sales, ItemType.Inventory) => INVENTORY_SALES,     // 600 - Yurt İçi Satışlar
            (InvoiceType.Sales, ItemType.Service) => SERVICE_SALES,         // 602 - Hizmet Satışları
            (InvoiceType.Sales, ItemType.Expense) => null,                  // ❌ Masraf satılmaz
            (InvoiceType.Sales, ItemType.FixedAsset) => null,               // ❌ Demirbaş satılmaz (normal işletme faaliyetinde)

            // ====================================
            // ALIŞ FATURALARI (Purchase)
            // ====================================
            (InvoiceType.Purchase, ItemType.Inventory) => INVENTORY_PURCHASE,     // 153 - Ticari Mallar
            (InvoiceType.Purchase, ItemType.Service) => EXPENSE_PURCHASE,         // 770 - Genel Üretim Giderleri (Hizmet alımı)
            (InvoiceType.Purchase, ItemType.Expense) => EXPENSE_PURCHASE,         // 770 - Genel Üretim Giderleri
            (InvoiceType.Purchase, ItemType.FixedAsset) => FIXED_ASSET_PURCHASE,  // 255 - Demirbaşlar

            // ====================================
            // SATIŞ İADESİ (SalesReturn)
            // ====================================
            (InvoiceType.SalesReturn, ItemType.Inventory) => INVENTORY_SALES,     // 600 (ters kayıt)
            (InvoiceType.SalesReturn, ItemType.Service) => SERVICE_SALES,         // 602 (ters kayıt)
            (InvoiceType.SalesReturn, ItemType.Expense) => null,                  // ❌ Geçersiz
            (InvoiceType.SalesReturn, ItemType.FixedAsset) => null,               // ❌ Geçersiz

            // ====================================
            // ALIŞ İADESİ (PurchaseReturn)
            // ====================================
            (InvoiceType.PurchaseReturn, ItemType.Inventory) => INVENTORY_PURCHASE,     // 153 (ters kayıt)
            (InvoiceType.PurchaseReturn, ItemType.Service) => EXPENSE_PURCHASE,         // 770 (ters kayıt)
            (InvoiceType.PurchaseReturn, ItemType.Expense) => EXPENSE_PURCHASE,         // 770 (ters kayıt)
            (InvoiceType.PurchaseReturn, ItemType.FixedAsset) => FIXED_ASSET_PURCHASE,  // 255 (ters kayıt)

            _ => null  // Tanımlanmamış kombinasyon
        };
    }

    /// <summary>
    /// Verilen fatura tipi ve item tipi kombinasyonunun geçerli olup olmadığını kontrol eder.
    /// </summary>
    public static bool IsValidCombination(InvoiceType invoiceType, ItemType itemType)
    {
        return GetAccountCode(invoiceType, itemType) != null;
    }

    /// <summary>
    /// Muhasebe kodu açıklamalarını döndürür (raporlama için).
    /// </summary>
    public static string GetAccountCodeDescription(string? accountCode)
    {
        return accountCode switch
        {
            INVENTORY_PURCHASE => "153 - Ticari Mallar",
            FIXED_ASSET_PURCHASE => "255 - Demirbaşlar",
            EXPENSE_PURCHASE => "770 - Genel Üretim Giderleri",
            INVENTORY_SALES => "600 - Yurt İçi Satışlar",
            SERVICE_SALES => "602 - Hizmet Satışları",
            _ => "Tanımsız"
        };
    }
}
