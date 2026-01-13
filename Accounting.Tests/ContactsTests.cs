using Accounting.Application.Contacts.Commands.Create;
using Accounting.Application.Contacts.Commands.Update;
using Accounting.Application.Contacts.Commands.Delete;
using Accounting.Application.Contacts.Queries.GetById;
using Accounting.Application.Contacts.Queries.List;
using Accounting.Application.Contacts.Queries.Dto;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Accounting.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Accounting.Tests;

public class ContactsTests
{
    private readonly DbContextOptions<AppDbContext> _options;

    public ContactsTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task CreateContact_ShouldSucceed_WhenValidCustomer()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        await db.SaveChangesAsync();

        // CreateContactHandler sadece db alır
        var handler = new CreateContactHandler(db);
        
        var command = new CreateContactCommand(
            BranchId: 1,
            IsCustomer: true,
            IsVendor: false,
            IsEmployee: false,
            IsRetail: false,
            Name: "Test Customer",
            Email: "customer@test.com",
            Phone: "5551234567",
            Iban: null,
            Address: "Test Address",
            City: "Istanbul",
            District: "Kadikoy",
            CompanyDetails: new CompanyDetailsDto("1234567890", "Test Office", null, null),
            PersonDetails: null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(0, result.Id);
        var contact = await db.Contacts.FindAsync(result.Id);
        Assert.NotNull(contact);
        Assert.Equal("Test Customer", contact.Name);
        Assert.True(contact.IsCustomer);
        Assert.False(contact.IsVendor);
        Assert.Equal("1234567890", contact.CompanyDetails?.TaxNumber);
    }

    [Fact]
    public async Task UpdateContact_ShouldSucceed()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        var contact = new Contact
        {
            BranchId = 1,
            Name = "Old Name",
            Code = "CONT-001",
            IsCustomer = true,
            IsVendor = false,
            RowVersion = Array.Empty<byte>()
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var handler = new UpdateContactHandler(db, userService);
        
        var command = new UpdateContactCommand(
            Id: contact.Id,
            IsCustomer: true,
            IsVendor: true,
            IsEmployee: false,
            IsRetail: false,
            Name: "Updated Name",
            Email: "updated@test.com",
            Phone: "5559876543",
            Iban: null,
            Address: "Updated Address",
            City: "Ankara",
            District: "Cankaya",
            CompanyDetails: new CompanyDetailsDto("9876543210", "Updated Office", null, null),
            PersonDetails: null,
            RowVersion: Convert.ToBase64String(contact.RowVersion)
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Updated Name", result.Name);
        Assert.True(result.IsVendor);
    }

    [Fact]
    public async Task SoftDeleteContact_ShouldMarkAsDeleted()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        var contact = new Contact
        {
            BranchId = 1,
            Name = "To Delete",
            Code = "DEL-001",
            IsCustomer = true,
            IsVendor = false,
            RowVersion = Array.Empty<byte>()
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var handler = new SoftDeleteContactHandler(db, userService);
        await handler.Handle(new SoftDeleteContactCommand(contact.Id, Convert.ToBase64String(contact.RowVersion)), CancellationToken.None);

        var deletedContact = await db.Contacts.IgnoreQueryFilters().FirstAsync(c => c.Id == contact.Id);
        Assert.True(deletedContact.IsDeleted);
    }

    [Fact]
    public async Task GetContactById_ShouldReturnContact()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        var contact = new Contact
        {
            BranchId = 1,
            Name = "Test Contact",
            Code = "TST-001",
            IsCustomer = true,
            IsVendor = false,
            RowVersion = Array.Empty<byte>()
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var handler = new GetContactByIdHandler(db, userService);
        var result = await handler.Handle(new GetContactByIdQuery(contact.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Test Contact", result.Name);
    }

    [Fact]
    public async Task ListContacts_ShouldReturnPagedResults()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        for (int i = 1; i <= 5; i++)
        {
            db.Contacts.Add(new Contact
            {
                BranchId = 1,
                Name = $"Contact {i}",
                Code = $"CONT-{i:000}",
                IsCustomer = true,
                IsVendor = false,
                RowVersion = Array.Empty<byte>()
            });
        }
        await db.SaveChangesAsync();

        var handler = new ListContactsHandler(db, userService);
        var query = new ListContactsQuery(
            BranchId: 1, 
            Search: null, 
            IsCustomer: null, 
            IsVendor: null, 
            IsEmployee: null, 
            IsRetail: null,
            Page: 1,
            PageSize: 3
        );
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
    }
}
