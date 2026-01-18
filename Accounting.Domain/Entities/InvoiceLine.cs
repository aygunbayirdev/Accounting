using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

public class InvoiceLine : IHasTimestamps, ISoftDeletable
{
    public int Id { get; set; }

    // FK'ler
    public int InvoiceId { get; set; }
    public int? ItemId { get; set; }

    // ✅ Snapshot alanlar (o anın kopyası)
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string Unit { get; set; } = "adet";   // örn: adet, kg, lt

    // Muhasebe Kodu (Snapshot - Item'dan kopyalanır)
    public string? AccountCode { get; set; }  // İşlemin türüne göre (Purchase → PurchaseAccountCode, Sales → SalesAccountCode)

    // Snapshot alanlar (fiyat/KDV o anki kurallarla sabitlenir)
    public decimal Qty { get; set; }        // 18,3
    public decimal UnitPrice { get; set; }  // 18,4
    public int VatRate { get; set; }        // 0..100

    // Türemiş/saklanan tutarlar (AwayFromZero, 2 hane)
    public decimal Gross { get; set; }      // Brüt (Qty * Price)

    public decimal DiscountRate { get; set; }   // İskonto Oranı (%)
    public decimal DiscountAmount { get; set; } // İskonto Tutarı

    public decimal Net { get; set; }        // Net/Matrah (Gross - Discount)

    public decimal Vat { get; set; }        // KDV Tutarı (Net * VatRate)

    public int WithholdingRate { get; set; }    // Tevkifat Oranı (%) (Örn: 50 = 5/10)
    public decimal WithholdingAmount { get; set; } // Tevkifat Tutarı (Vat * Rate)

    public decimal GrandTotal { get; set; } // Genel Toplam (Net + Vat)

    // Timestamps
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    // Navigations
    public Invoice Invoice { get; set; } = null!;
    public Item? Item { get; set; }
}
