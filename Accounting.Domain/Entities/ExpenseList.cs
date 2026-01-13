using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

public class ExpenseList : IHasTimestamps, ISoftDeletable, IHasRowVersion, IHasBranch
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string Name { get; set; } = "Masraf Listesi";

    // eski CreatedUtc yerine unified:
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public ExpenseListStatus Status { get; set; } = ExpenseListStatus.Draft;
    public int? PostedInvoiceId { get; set; }

    public ICollection<ExpenseLine> Lines { get; set; } = new List<ExpenseLine>();

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public Branch Branch { get; set; } = null!;
}
