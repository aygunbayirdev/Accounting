using Accounting.Application.Common.Interfaces;
using Accounting.Application.StockMovements.Commands.Create;
using Accounting.Application.StockMovements.Commands.Transfer;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;
using Accounting.Application.Common.Exceptions;

namespace Accounting.Tests;

public class StockMovementTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly AppDbContext _db;

    public StockMovementTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(x => x.BranchId).Returns(1);
        _currentUserServiceMock.Setup(x => x.UserId).Returns(1);

        var audit = new AuditSaveChangesInterceptor(_currentUserServiceMock.Object);
        _db = new AppDbContext(options, audit, _currentUserServiceMock.Object);

        SeedData();
    }

    private void SeedData()
    {
        _db.Branches.Add(new Branch { Id = 1, Name = "Main", Code = "B1" });
        _db.Warehouses.Add(new Warehouse { Id = 1, BranchId = 1, Name = "Main Warehouse", Code = "WH1" });
        _db.Warehouses.Add(new Warehouse { Id = 2, BranchId = 1, Name = "Secondary Warehouse", Code = "WH2" });
        _db.Items.Add(new Item { Id = 1, Name = "Item A", Code = "ITM001", Unit = "PCS" });
        _db.SaveChanges();
    }

    [Fact]
    public async Task CreateStockMovement_ShouldSucceed_WhenInputIsValid()
    {
        var handler = new CreateStockMovementHandler(_db, _currentUserServiceMock.Object);

        var command = new CreateStockMovementCommand(
            WarehouseId: 1,
            ItemId: 1,
            Type: StockMovementType.AdjustmentIn, // Changed from Entry
            Quantity: 10.5m,
            TransactionDateUtc: DateTime.UtcNow,
            Note: "Initial Stock",
            InvoiceId: null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, result.Id);
        
        var dbMovement = await _db.StockMovements.FindAsync(result.Id);
        Assert.NotNull(dbMovement);
        Assert.Equal(10.5m, dbMovement.Quantity);
        Assert.Equal(StockMovementType.AdjustmentIn, dbMovement.Type);
        Assert.Equal(1, dbMovement.BranchId); // From CurrentUser
    }

    [Fact]
    public async Task CreateStockMovement_ShouldFail_WhenWarehouseNotFound()
    {
        var handler = new CreateStockMovementHandler(_db, _currentUserServiceMock.Object);
        var command = new CreateStockMovementCommand(99, 1, StockMovementType.AdjustmentIn, 10.00m, DateTime.UtcNow, null);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task TransferStock_ShouldSucceed()
    {
        // Arrange: Initial stock in Source
        _db.Stocks.Add(new Stock 
        {
            BranchId = 1,
            WarehouseId = 1,
            ItemId = 1,
            Quantity = 100,
            RowVersion = Array.Empty<byte>()
        });
        
        _db.StockMovements.Add(new StockMovement 
        { 
            BranchId = 1,
            WarehouseId = 1, 
            ItemId = 1, 
            Type = StockMovementType.AdjustmentIn, 
            Quantity = 100, 
            TransactionDateUtc = DateTime.UtcNow,
            RowVersion = Array.Empty<byte>()
        });
        await _db.SaveChangesAsync();

        var handler = new TransferStockHandler(_db); // Removed mock arg

        var command = new TransferStockCommand(
            SourceWarehouseId: 1,
            TargetWarehouseId: 2,
            ItemId: 1,
            Quantity: 50.00m,
            TransactionDateUtc: DateTime.UtcNow,
            Description: "Transfer half"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotEqual(0, result.InMovementId); 
        
        var movements = await _db.StockMovements.Where(x => x.Note.Contains("Transfer half")).ToListAsync();
        Assert.Equal(2, movements.Count); // One entry, one exit

        var exit = movements.FirstOrDefault(x => x.Type == StockMovementType.TransferOut);
        var entry = movements.FirstOrDefault(x => x.Type == StockMovementType.TransferIn);

        Assert.NotNull(exit);
        Assert.NotNull(entry);
        Assert.Equal(1, exit.WarehouseId);
        Assert.Equal(2, entry.WarehouseId);
        Assert.Equal(50, exit.Quantity);
        Assert.Equal(50, entry.Quantity);
    }

    [Fact]
    public async Task TransferStock_ShouldFail_WhenInsufficientStock()
    {
        // No initial stock
        var handler = new TransferStockHandler(_db); // Removed mock arg
        var command = new TransferStockCommand(1, 2, 1, 50.00m, DateTime.UtcNow, "Transfer");

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(command, CancellationToken.None));
    }
}
