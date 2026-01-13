using Accounting.Domain.Common;

namespace Accounting.Domain.Entities;

public class Stock : IHasTimestamps, ISoftDeletable, IHasRowVersion, IHasBranch
{
    public int Id { get; set; }

    public int BranchId { get; set; }
    public int WarehouseId { get; set; }
    public int ItemId { get; set; }

    /// <summary>
    /// Anlık stok miktarı (snapshot). 3 ondalık (kg/lt gibi senaryolar).
    /// </summary>
    public decimal Quantity { get; set; }

    // audit + soft delete + concurrency
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigations
    public Branch Branch { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public Item Item { get; set; } = null!;
}
