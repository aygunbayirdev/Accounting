using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

public class Order : IHasTimestamps, ISoftDeletable, IHasRowVersion, IHasBranch
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int ContactId { get; set; }
    public int? CreatedByUserId { get; set; } // Nullable because existing orders don't have it

    public string OrderNumber { get; set; } = null!;
    public DateTime DateUtc { get; set; }
    public InvoiceType Type { get; set; } // Sales/Purchase (Using InvoiceType for simplicity or create OrderType?) -> Reusing InvoiceType is fine as per plan
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public string Currency { get; set; } = "TRY";
    public string? Description { get; set; }

    // Totals
    public decimal TotalNet { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalGross { get; set; }

    // Audit
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigations
    public Branch Branch { get; set; } = null!;
    public Contact Contact { get; set; } = null!;
    public User? CreatedByUser { get; set; }
    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
}
