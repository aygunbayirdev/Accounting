using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence.Configurations; // ApplyRowVersion/ApplySoftDelete
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounting.Infrastructure.Persistence.Configurations;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> b)
    {
        b.ToTable("Contacts");
        b.HasKey(x => x.Id);

        b.Property(x => x.Code)
         .IsRequired()
         .HasMaxLength(32)
         .IsUnicode(true);

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        
        // Removed: TaxNo
        // Added: Flags 
        // (EF Core defaults bool to bit, no special config needed unless we want default values)

        b.Property(x => x.Email).HasMaxLength(320);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.Iban).HasMaxLength(34);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.City).HasMaxLength(200);
        b.Property(x => x.District).HasMaxLength(200);
        
        // Relations
        b.HasOne(c => c.Branch)
            .WithMany()
            .HasForeignKey(c => c.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composition 1:0..1
        b.OwnsOne(c => c.CompanyDetails, cb =>
        {
            cb.ToTable("CompanyDetails");
            cb.WithOwner().HasForeignKey("ContactId");
            cb.Property(x => x.TaxNumber).HasMaxLength(10);
            cb.Property(x => x.TaxOffice).HasMaxLength(100);
            cb.Property(x => x.MersisNo).HasMaxLength(20);
            cb.Property(x => x.TicaretSicilNo).HasMaxLength(20);
        });

        b.OwnsOne(c => c.PersonDetails, pb =>
        {
            pb.ToTable("PersonDetails");
            pb.WithOwner().HasForeignKey("ContactId");
            pb.Property(x => x.Tckn).HasMaxLength(11);
            pb.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            pb.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            pb.Property(x => x.Title).HasMaxLength(100);
            pb.Property(x => x.Department).HasMaxLength(100);
        });

        // audit
        b.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()")
            .ValueGeneratedOnAdd()
            .IsRequired();

        // concurrency + soft delete (tek merkezden)
        b.ApplyRowVersion();
        b.ApplySoftDelete();

        // indexes
        b.HasIndex(x => x.Name).HasDatabaseName("IX_Contacts_Name");
        b.HasIndex(x => x.Code)
            .HasDatabaseName("UX_Contacts_Code")
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        
        // Index for FLAGS
        b.HasIndex(x => x.IsCustomer);
        b.HasIndex(x => x.IsVendor);
        b.HasIndex(x => x.IsEmployee);
        b.HasIndex(x => x.BranchId).HasDatabaseName("IX_Contacts_BranchId");

    }
}
