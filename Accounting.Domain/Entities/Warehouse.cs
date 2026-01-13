using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

public class Warehouse : IHasTimestamps, ISoftDeletable, IHasRowVersion, IHasBranch
{
    public int Id { get; set; }

    public int BranchId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsDefault { get; set; } // şubenin ana deposu mu

    // audit + soft delete + concurrency
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigations
    public Branch Branch { get; set; } = null!;
}
