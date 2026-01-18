using Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounting.Infrastructure.Persistence.Configurations;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> b)
    {
        b.ToTable("Items");
        b.HasKey(x => x.Id);

        // BranchId kaldırıldı - Global entity

        b.Property(x => x.Name).IsRequired().HasMaxLength(256);
        b.Property(x => x.Code).IsRequired().HasMaxLength(64);
        b.Property(x => x.Type).IsRequired();
        b.Property(x => x.Unit).IsRequired().HasMaxLength(16);
        b.Property(x => x.VatRate).IsRequired();

        b.Property(x => x.PurchasePrice).HasColumnType("decimal(18,4)");
        b.Property(x => x.SalesPrice).HasColumnType("decimal(18,4)");

        // Muhasebe kodları (TDHP)
        b.Property(x => x.PurchaseAccountCode).HasMaxLength(16);
        b.Property(x => x.SalesAccountCode).HasMaxLength(16);
        b.Property(x => x.UsefulLifeYears);

        // Code artık globally unique
        b.HasIndex(x => x.Code).IsUnique();

        // Timestamps
        b.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()")
            .ValueGeneratedOnAdd()
            .IsRequired();

        // Soft delete
        b.Property(x => x.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();
        b.Property(x => x.DeletedAtUtc);
        b.HasQueryFilter(x => !x.IsDeleted);

        // Concurrency
        b.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsRequired();

        // Relations
        // Branch relation KALDIRILDI

        b.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Check constraints
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Item_VatRate_Range", "[VatRate] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_Item_PurchasePrice_Positive", "[PurchasePrice] IS NULL OR [PurchasePrice] >= 0");
            t.HasCheckConstraint("CK_Item_SalesPrice_Positive", "[SalesPrice] IS NULL OR [SalesPrice] >= 0");
        });
    }
}
