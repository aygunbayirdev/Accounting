using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

public class ExpenseDefinition : IHasTimestamps, ISoftDeletable, IHasRowVersion, IHasBranch
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string Code { get; set; } = null!;    // Unique per branch, max 32
    public string Name { get; set; } = null!;    // max 128
    public int DefaultVatRate { get; set; }      // 0..100 (yüzde)
    public bool IsActive { get; set; } = true;

    // Timestamps
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    // Concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public Branch Branch { get; set; } = null!;
}
