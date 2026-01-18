using Accounting.Domain.Common;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Entities;

// IHasBranch KALDIRILDI - Artık global
public class Item : IHasTimestamps, ISoftDeletable, IHasRowVersion
{
    public int Id { get; set; }
    // BranchId KALDIRILDI - Global entity
    public int? CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public ItemType Type { get; set; } = ItemType.Inventory;
    public string Unit { get; set; } = "adet";
    public int VatRate { get; set; } = 20;
    public int? DefaultWithholdingRate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? SalesPrice { get; set; }

    public string? PurchaseAccountCode { get; set; }
    public string? SalesAccountCode { get; set; }
    public int? UsefulLifeYears { get; set; }

    // audit + soft delete + concurrency
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigations
    // Branch navigation KALDIRILDI
    public Category? Category { get; set; }
}
