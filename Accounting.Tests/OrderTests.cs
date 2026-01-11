using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Reports.Queries;
using Accounting.Application.Orders.Commands.Approve;
using Accounting.Application.Orders.Commands.Create;
using Accounting.Application.Orders.Commands.CreateInvoice;
using Accounting.Application.Orders.Commands.Update;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Accounting.Tests.Common;

namespace Accounting.Tests;

public class OrderTests
{
    private AppDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var userService = new FakeCurrentUserService(1);
        return new AppDbContext(options, new AuditSaveChangesInterceptor(userService), userService);
    }

    [Fact]
    public async Task CreateOrder_ShouldSucceed()
    {
        var db = GetDbContext();
        var handler = new CreateOrderHandler(db, new FakeCurrentUserService(1));
        var date = DateTime.UtcNow;
        var r = new CreateOrderCommand(
            ContactId: 10,
            DateUtc: date,
            Type: InvoiceType.Sales,
            Currency: "TRY",
            Description: "Test Order",
            Lines: new List<CreateOrderLineDto>
            {
                new(1, "Item A", "10", "100", 20)
            }
        );

        var result = await handler.Handle(r, CancellationToken.None);

        Assert.NotEqual(0, result.Id);
        Assert.Equal(OrderStatus.Draft, result.Status);
        Assert.Equal(1000m, result.TotalNet); // 10 * 100
        Assert.Equal(1200m, result.TotalGross); // 1000 + 20% VAT
        
        var inDb = await db.Orders.Include(x => x.Lines).FirstAsync(x => x.Id == result.Id);
        Assert.Single(inDb.Lines);
    }

    [Fact]
    public async Task UpdateOrder_ShouldSucceed_WhenDraft()
    {
        var db = GetDbContext();
        var rowVer = new byte[] { 1 };
        var order = new Order 
        { 
            BranchId = 1, ContactId = 1, OrderNumber = "ORD001", 
            DateUtc = DateTime.UtcNow, Status = OrderStatus.Draft, 
            RowVersion = rowVer 
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new UpdateOrderHandler(db, new FakeCurrentUserService(1));
        var cmd = new UpdateOrderCommand(
            order.Id,
            ContactId: 2, // Changing contact
            DateUtc: DateTime.UtcNow,
            Description: "Updated",
            Lines: new List<UpdateOrderLineDto>(), // Clearing lines
            RowVersion: Convert.ToBase64String(order.RowVersion)
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("Updated", result.Description);
        Assert.Equal(2, result.ContactId);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task UpdateOrder_ShouldFail_WhenApproved()
    {
        var db = GetDbContext();
        var rowVer = new byte[] { 1 };
        var order = new Order 
        { 
            BranchId = 1, ContactId = 1, OrderNumber = "ORD002", 
            DateUtc = DateTime.UtcNow, Status = OrderStatus.Approved, // Approved!
            RowVersion = rowVer 
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new UpdateOrderHandler(db, new FakeCurrentUserService(1));
        var cmd = new UpdateOrderCommand(
            order.Id, 1, DateTime.UtcNow, "Try Update", new(), Convert.ToBase64String(order.RowVersion)
        );

        await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task ApproveOrder_ShouldSucceed()
    {
        var db = GetDbContext();
        var rowVer = new byte[] { 1 };
        var order = new Order 
        { 
            BranchId = 1, ContactId = 1, OrderNumber = "ORD003", 
            DateUtc = DateTime.UtcNow, Status = OrderStatus.Draft, 
            RowVersion = rowVer 
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new ApproveOrderHandler(db, new FakeStockService(), new FakeCurrentUserService(1));
        var result = await handler.Handle(new ApproveOrderCommand(order.Id, rowVer), CancellationToken.None);

        Assert.True(result);
        var inDb = await db.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Approved, inDb!.Status);
    }

    [Fact]
    public async Task CreateInvoiceFromOrder_ShouldSucceed_WhenApproved()
    {
        // Assemble
        var db = GetDbContext();
        var rowVer = new byte[] { 1 };
        var order = new Order 
        { 
            BranchId = 1, ContactId = 5, OrderNumber = "ORD004", 
            DateUtc = DateTime.UtcNow, Status = OrderStatus.Approved, // Must be Approved
            Type = InvoiceType.Sales,
            Currency = "USD",
            TotalNet = 100, TotalVat = 18, TotalGross = 118,
            RowVersion = rowVer 
        };
        order.Lines.Add(new OrderLine 
        { 
            Description = "Product A", Quantity = 1, UnitPrice = 100, VatRate = 18, Total = 100 
        });

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Act
        var handler = new CreateInvoiceFromOrderHandler(db, new FakeCurrentUserService(1));
        var invoiceId = await handler.Handle(new CreateInvoiceFromOrderCommand(order.Id), CancellationToken.None);

        // Assert
        Assert.NotEqual(0, invoiceId);
        
        var invoice = await db.Invoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == invoiceId);
        Assert.NotNull(invoice);
        Assert.Equal(order.Id, invoice.OrderId);
        Assert.Equal(5, invoice.ContactId);
        Assert.Equal("USD", invoice.Currency);
        Assert.Single(invoice.Lines);
        Assert.Equal("Product A", invoice.Lines[0].ItemName);

        // Check Order Status Update
        var updatedOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Invoiced, updatedOrder!.Status);
    }
}

public class FakeStockService : IStockService
{
    public Task<List<ItemStockDto>> GetStockStatusAsync(List<int> itemIds, CancellationToken ct) 
        => Task.FromResult(new List<ItemStockDto>());
    public Task<ItemStockDto> GetItemStockAsync(int itemId, CancellationToken ct) 
        => Task.FromResult(new ItemStockDto(itemId, 0, 0, 0, 0));
    public Task ValidateStockAvailabilityAsync(int itemId, decimal quantityRequired, CancellationToken ct) 
        => Task.CompletedTask;
    public Task ValidateBatchStockAvailabilityAsync(Dictionary<int, decimal> stockRequirements, CancellationToken ct) 
        => Task.CompletedTask;
}
