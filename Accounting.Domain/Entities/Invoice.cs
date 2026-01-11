using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

public class Invoice : IHasTimestamps, ISoftDeletable, IHasRowVersion, IHasBranch
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int ContactId { get; set; }
    public int? OrderId { get; set; } // Link to Order
    public InvoiceType Type { get; set; } = InvoiceType.Sales;
    public DateTime DateUtc { get; set; } = DateTime.UtcNow;
    public string InvoiceNumber { get; set; } = null!;
    public string Currency { get; set; } = "TRY";
    public decimal CurrencyRate { get; set; } = 1.0m; // İşlem tarihindeki kur

    // İrsaliye Bilgileri
    public string? WaybillNumber { get; set; }
    public DateTime? WaybillDateUtc { get; set; }

    // Ödeme Vadesi
    public DateTime? PaymentDueDateUtc { get; set; }

    // Toplamlar
    public decimal TotalLineGross { get; set; } // Satırların Brüt Toplamı (İskonto öncesi)
    public decimal TotalDiscount { get; set; }  // Toplam İskonto
    public decimal TotalNet { get; set; }       // Matrah (TotalLineGross - TotalDiscount)
    public decimal TotalVat { get; set; }       // Toplam KDV
    public decimal TotalWithholding { get; set; } // Toplam Tevkifat
    public decimal TotalGross { get; set; }     // Genel Toplam (Vergiler Dahil, Tevkifat Düşülmemiş)
    // NOT: Fatura Dip Toplamı = TotalGross
    // Cari Alacağına işlenen = TotalGross - TotalWithholding
    
    public decimal Balance { get; set; }        // Kalan Bakiye

    public List<InvoiceLine> Lines { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    public Contact Contact { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Order? Order { get; set; } // Sipariş kaynaklı faturalar için
}
