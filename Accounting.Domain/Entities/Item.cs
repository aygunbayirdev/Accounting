using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

public class Item : IHasTimestamps, ISoftDeletable, IHasRowVersion, IHasBranch
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int? CategoryId { get; set; } // Optional category
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public ItemType Type { get; set; } = ItemType.Inventory;
    public string Unit { get; set; } = "adet";
    public int VatRate { get; set; } = 20;
    public int? DefaultWithholdingRate { get; set; } // Varsayılan Tevkifat Oranı (%) - Nullable
    public decimal? PurchasePrice { get; set; } // Alış Fiyatı (Maliyet)
    public decimal? SalesPrice { get; set; }    // Satış Fiyatı (Liste Fiyatı)

    // audit + soft delete + concurrency
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigations
    public Branch Branch { get; set; } = null!;
    public Category? Category { get; set; }
}
