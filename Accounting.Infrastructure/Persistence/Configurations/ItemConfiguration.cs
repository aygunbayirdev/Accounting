using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence.Configurations; // ApplyRowVersion/ApplySoftDelete
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounting.Infrastructure.Persistence.Configurations;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> b)
    {
        b.ToTable("Items");
        b.HasKey(x => x.Id);

        b.Property(x => x.Code).IsRequired().HasMaxLength(64);
        b.Property(x => x.Name).IsRequired().HasMaxLength(256);
        b.Property(x => x.Unit).IsRequired().HasMaxLength(16);
        b.Property(x => x.VatRate).IsRequired();

        b.Property(e => e.PurchaseAccountCode).HasMaxLength(16);
        b.Property(e => e.SalesAccountCode).HasMaxLength(16);
        b.Property(e => e.UsefulLifeYears);

        b.Property(x => x.PurchasePrice).HasColumnType("decimal(18,4)");
        b.Property(x => x.SalesPrice).HasColumnType("decimal(18,4)");

        b.HasOne(i => i.Branch)
            .WithMany()
            .HasForeignKey(i => i.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // audit
        b.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()")
            .ValueGeneratedOnAdd()
            .IsRequired();

        // concurrency + soft delete
        b.ApplyRowVersion();
        b.ApplySoftDelete();

        // indexes / constraints
        b.HasIndex(x => x.Name).HasDatabaseName("IX_Items_Name");
        b.HasIndex(x => x.Code)
            .HasDatabaseName("UX_Items_Code")
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => x.BranchId).HasDatabaseName("IX_Items_BranchId");
        b.HasIndex(x => x.CategoryId).HasDatabaseName("IX_Items_CategoryId");

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Item_VatRate_Range", "[VatRate] BETWEEN 0 AND 100");
        });
    }
}
