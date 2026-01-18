using Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounting.Infrastructure.Persistence.Configurations;
public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.ToTable("Invoices");
        b.HasKey(x => x.Id);

        b.Property(x => x.InvoiceNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.Type)
            .HasConversion<int>()
            .IsRequired();
        b.Property(e => e.DocumentType)
            .HasConversion<int>()
            .IsRequired();
        b.Property(x => x.Currency).IsRequired().HasMaxLength(3).IsUnicode(false);
        b.Property(x => x.CurrencyRate).HasColumnType("decimal(18,4)"); // Opus Review
        b.Property(x => x.DateUtc).IsRequired();

        b.Property(x => x.WaybillNumber).HasMaxLength(50); // Opus Review

        b.Property(x => x.TotalNet).HasColumnType("decimal(18,2)");
        b.Property(x => x.TotalVat).HasColumnType("decimal(18,2)");
        b.Property(x => x.TotalGross).HasColumnType("decimal(18,2)");
        b.Property(x => x.Balance).HasColumnType("decimal(18,2)");

        // ✅ Aggregate tutarlılığı: hard delete'i önlemek için Restrict
        b.HasMany(x => x.Lines)
            .WithOne(l => l.Invoice)
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict); // was: Cascade

        // audit
        b.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()")
            .ValueGeneratedOnAdd()
            .IsRequired();

        b.ApplyRowVersion();
        b.ApplySoftDelete();

        // indexes
        b.HasIndex(x => x.DateUtc).HasDatabaseName("IX_Invoices_DateUtc");
        b.HasIndex(x => x.ContactId).HasDatabaseName("IX_Invoices_ContactId");
        b.HasIndex(x => x.Currency).HasDatabaseName("IX_Invoices_Currency");
        b.HasIndex(x => x.Type).HasDatabaseName("IX_Invoices_Type");
        b.HasIndex(i => i.BranchId).HasDatabaseName("IX_Invoices_BranchId");

        b.HasOne(i => i.Contact)
            .WithMany()
            .HasForeignKey(i => i.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Property(i => i.ContactId).IsRequired();

        b.HasOne(i => i.Branch)
            .WithMany()
            .HasForeignKey(i => i.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Order relationship (nullable - not all invoices come from orders)
        b.HasIndex(x => x.OrderId).HasDatabaseName("IX_Invoices_OrderId");

    }
}
