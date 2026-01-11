using Accounting.Domain.Common;


namespace Accounting.Domain.Entities;

public class Contact : IHasTimestamps, ISoftDeletable, IHasRowVersion, IHasBranch
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    
    // Flags (Roles)
    public bool IsCustomer { get; set; }
    public bool IsVendor { get; set; }
    public bool IsEmployee { get; set; }
    public bool IsRetail { get; set; } // Perakende

    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!; // Display Name (Company Name or First+Last)
    
    // Banking
    public string? Iban { get; set; }

    // Contact
    public string? Email { get; set; }
    public string? Phone { get; set; }
    
    // Address
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Address { get; set; }

    // unified timestamps
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    // soft delete + concurrency
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigations
    public Branch Branch { get; set; } = null!;
    
    // 1:0..1 Relationships
    public virtual CompanyDetails? CompanyDetails { get; set; }
    public virtual PersonDetails? PersonDetails { get; set; }
}
