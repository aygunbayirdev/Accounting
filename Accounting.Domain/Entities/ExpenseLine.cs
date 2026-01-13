using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

public class ExpenseLine : IHasTimestamps, ISoftDeletable, IHasRowVersion
{
    public int Id { get; set; }

    public int ExpenseListId { get; set; }

    public DateTime DateUtc { get; set; } = DateTime.UtcNow;

    public int? SupplierId { get; set; }
    public string Currency { get; set; } = "TRY";

    public decimal Amount { get; set; }
    public int VatRate { get; set; }

    public string? Category { get; set; }
    public string? Notes { get; set; }

    public int? PostedInvoiceId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // navigations
    public ExpenseList ExpenseList { get; set; } = null!;
}
