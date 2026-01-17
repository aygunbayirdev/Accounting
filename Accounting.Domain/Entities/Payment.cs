using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

public class Payment : IHasTimestamps, ISoftDeletable, IHasRowVersion, IHasBranch
{
    public int Id { get; set; }
    public int BranchId { get; set; }

    public int AccountId { get; set; }
    public int? ContactId { get; set; }
    public int? LinkedInvoiceId { get; set; }

    /// <summary>
    /// Çek/Senet tahsilatı veya ödemesinden oluşan payment'lar için referans.
    /// Manuel ödeme girişlerinde null.
    /// </summary>
    public int? ChequeId { get; set; }

    public DateTime DateUtc { get; set; } = DateTime.UtcNow;
    public PaymentDirection Direction { get; set; } = PaymentDirection.In;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string? Description { get; set; }  // Açıklama (opsiyonel)

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigations
    public CashBankAccount Account { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Contact? Contact { get; set; }
    public Invoice? LinkedInvoice { get; set; }
    public Cheque? Cheque { get; set; }
}
