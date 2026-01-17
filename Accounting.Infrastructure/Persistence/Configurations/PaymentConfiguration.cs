using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence.Configurations; // ApplyRowVersion/ApplySoftDelete
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounting.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments");
        b.HasKey(x => x.Id);

        b.Property(x => x.Direction).HasConversion<int>();
        b.Property(x => x.DateUtc).IsRequired();
        b.Property(x => x.Currency).IsRequired().HasMaxLength(3).IsUnicode(false);
        b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        b.Property(x => x.Description).HasMaxLength(200);

        // ✅ Navigations & FK davranışları: hepsi Restrict
        b.HasOne(p => p.Account)
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(p => p.Branch)
            .WithMany()
            .HasForeignKey(p => p.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(p => p.Contact)
            .WithMany()
            .HasForeignKey(p => p.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(p => p.LinkedInvoice)
            .WithMany()
            .HasForeignKey(p => p.LinkedInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cheque ilişkisi (opsiyonel - çek/senet kaynaklı ödemeler için)
        b.HasOne(p => p.Cheque)
            .WithMany()
            .HasForeignKey(p => p.ChequeId)
            .OnDelete(DeleteBehavior.Restrict);

        // audit
        b.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()")
            .ValueGeneratedOnAdd()
            .IsRequired();

        b.ApplyRowVersion();
        b.ApplySoftDelete();

        // indexes
        b.HasIndex(x => x.DateUtc).HasDatabaseName("IX_Payments_DateUtc");
        b.HasIndex(x => x.AccountId).HasDatabaseName("IX_Payments_AccountId");
        b.HasIndex(x => x.BranchId).HasDatabaseName("IX_Payments_BranchId");
        b.HasIndex(x => x.ContactId).HasDatabaseName("IX_Payments_ContactId");
        b.HasIndex(x => x.LinkedInvoiceId).HasDatabaseName("IX_Payments_LinkedInvoiceId");
        b.HasIndex(x => x.ChequeId).HasDatabaseName("IX_Payments_ChequeId");
        b.HasIndex(x => x.Currency).HasDatabaseName("IX_Payments_Currency");
    }
}

