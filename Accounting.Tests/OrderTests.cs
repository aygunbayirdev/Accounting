using Accounting.Application.Common.Interfaces;
using Accounting.Application.Orders.Commands.Create;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Accounting.Tests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Accounting.Tests;

public class OrderTests
{
    private readonly DbContextOptions<AppDbContext> _options;

    public OrderTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task CreateOrder_ShouldSucceed_WhenValid()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        // Seed
        db.Branches.Add(new Branch { Id = 1, Name = "Main Branch", Code = "BR-01" });
        db.Contacts.Add(new Contact { Id = 1, BranchId = 1, Name = "Test Client", Code = "C-01" });
        db.Items.Add(new Item { Id = 10, BranchId = 1, Name = "Item A", Code = "I-01", Unit = "adet", VatRate = 20 });
        await db.SaveChangesAsync();

        var handler = new CreateOrderHandler(db, userService);

        var command = new CreateOrderCommand(
            ContactId: 1,
            DateUtc: DateTime.UtcNow,
            Type: InvoiceType.Sales,
            Currency: "TRY",
            Description: "Test Order",
            Lines: new List<CreateOrderLineDto>
            {
                new CreateOrderLineDto(10, "Item A", "5", "100", 20)
            }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("000001", result.OrderNumber);
        Assert.Single(result.Lines);
        Assert.Equal(500m, result.Lines[0].Total); // 5 * 100
        Assert.Equal(600m, result.TotalGross); // 500 + 20% VAT (100) = 600
    }
}
