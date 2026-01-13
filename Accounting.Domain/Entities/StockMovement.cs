using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

public class StockMovement : IHasTimestamps, ISoftDeletable, IHasRowVersion, IHasBranch
{
    public int Id { get; set; }

    public int BranchId { get; set; }
    public int WarehouseId { get; set; }
    public int ItemId { get; set; }

    /// <summary>
    /// Fatura kaynaklı stok hareketleri için referans.
    /// Manuel stok hareketlerinde null olabilir.
    /// </summary>
    public int? InvoiceId { get; set; }

    public StockMovementType Type { get; set; }

    /// <summary>
    /// Hareket miktarı (pozitif tutulur). Yön Type'tan belirlenir.
    /// </summary>
    public decimal Quantity { get; set; }

    public DateTime TransactionDateUtc { get; set; }
    public string? Note { get; set; }

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
    public Invoice? Invoice { get; set; }
}
