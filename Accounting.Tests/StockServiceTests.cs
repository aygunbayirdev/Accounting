using Accounting.Application.Common.Interfaces;
using Accounting.Application.Services;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Accounting.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Accounting.Tests;

public class StockServiceTests
{
    private readonly DbContextOptions<AppDbContext> _options;

    public StockServiceTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task GetItemStockAsync_ShouldCalculateCorrectly()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        // Seed
        db.Branches.Add(new Branch { Id = 1, Name = "Main Branch", Code = "BR-01" });
        db.Contacts.Add(new Contact { Id = 1, BranchId = 1, Name = "Contact", Code = "C-01" });
        db.Items.Add(new Item { Id = 10, BranchId = 1, Name = "Item A", Code = "I-01" });
        
        // Purchase Invoice (In)
        var inv1 = new Invoice
        {
            BranchId = 1, ContactId = 1, InvoiceNumber = "INV-01", DateUtc = DateTime.UtcNow,
            Type = InvoiceType.Purchase, RowVersion = Array.Empty<byte>()
        };
        inv1.Lines.Add(new InvoiceLine { ItemId = 10, ItemCode = "I-01", ItemName = "Item A", Unit="adet", Qty = 10, Net = 100, Gross = 120 });
        db.Invoices.Add(inv1);

        // Sales Invoice (Out)
        var inv2 = new Invoice
        {
            BranchId = 1, ContactId = 1, InvoiceNumber = "INV-02", DateUtc = DateTime.UtcNow,
            Type = InvoiceType.Sales, RowVersion = Array.Empty<byte>()
        };
        inv2.Lines.Add(new InvoiceLine { ItemId = 10, ItemCode = "I-01", ItemName = "Item A", Unit="adet", Qty = 3, Net = 30, Gross = 36 });
        db.Invoices.Add(inv2);

        // Approved Sales Order (Reserved)
        var order = new Order
        {
            BranchId = 1, ContactId = 1, OrderNumber = "ORD-01", DateUtc = DateTime.UtcNow,
            Type = InvoiceType.Sales, Status = OrderStatus.Approved, RowVersion = Array.Empty<byte>()
        };
        order.Lines.Add(new OrderLine { ItemId = 10, Description = "Item A", Quantity = 2, UnitPrice = 10, Total = 20 });
        db.Orders.Add(order);

        await db.SaveChangesAsync();

        var service = new StockService(db);

        var stock = await service.GetItemStockAsync(10, CancellationToken.None);

        // In: 10
        // Out: 3
        // Reserved: 2
        // Available: (10 - 3) - 2 = 5
        Assert.Equal(10, stock.QuantityIn);
        Assert.Equal(3, stock.QuantityOut);
        Assert.Equal(2, stock.QuantityReserved);
        Assert.Equal(5, stock.QuantityAvailable);
    }

    [Fact]
    public async Task ValidateStockAvailability_ShouldThrow_WhenInsufficient()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        // Seed only 5 items in stock
        db.Branches.Add(new Branch { Id = 1, Name = "Main Branch", Code = "BR-01" });
        db.Items.Add(new Item { Id = 10, BranchId = 1, Name = "Item A", Code = "I-01" });
        var inv = new Invoice { BranchId = 1, ContactId = 1, Type = InvoiceType.Purchase, InvoiceNumber="INV-01", RowVersion = Array.Empty<byte>() };
        inv.Lines.Add(new InvoiceLine { ItemId = 10, ItemCode = "I-01", ItemName = "Item A", Unit="adet", Qty = 5, Net=50, Gross=60 });
        db.Invoices.Add(inv);
        await db.SaveChangesAsync();

        var service = new StockService(db);

        // Request 10 -> Should Fail
        await Assert.ThrowsAsync<Accounting.Application.Common.Exceptions.BusinessRuleException>(async () =>
        {
            await service.ValidateStockAvailabilityAsync(10, 10, CancellationToken.None);
        });
    }
}
