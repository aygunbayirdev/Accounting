using Accounting.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Accounting.Application.StockMovements.Commands.Transfer;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;

using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors; // Added this one explicitly as it was missing or implicit
using Xunit;
using Accounting.Tests.Common;

namespace Accounting.Tests;

public class TransferStockHandlerTests
{
    private AppDbContext GetDbContext()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
            .Options;

        var userService = new FakeCurrentUserService(1);
        return new AppDbContext(options, new AuditSaveChangesInterceptor(userService), userService);
    }

    [Fact]
    public async Task TransferStock_ShouldSucceed_WhenValid()
    {
        // Arrange
        var db = GetDbContext();
        
        // Şube, Depolar, Item ve Stok (Kaynak)
        var rowVer = new byte[] { 1 };
        var branch = new Branch { Id = 1, Name = "Main Branch", Code = "MAIN", RowVersion = rowVer };
        var w1 = new Warehouse { Id = 1, BranchId = 1, Name = "Source", Code = "W1", RowVersion = rowVer };
        var w2 = new Warehouse { Id = 2, BranchId = 1, Name = "Target", Code = "W2", RowVersion = rowVer };
        var item = new Item { Id = 10, BranchId = 1, Name = "Item X", Code = "ITM01", RowVersion = rowVer };
        
        // 100 Adet Stok
        var stock1 = new Stock { BranchId = 1, WarehouseId = 1, ItemId = 10, Quantity = 100m, RowVersion = rowVer };

        db.Branches.Add(branch);
        db.Warehouses.AddRange(w1, w2);
        db.Items.Add(item);
        db.Stocks.Add(stock1);
        await db.SaveChangesAsync();

        var handler = new TransferStockHandler(db);
        var command = new TransferStockCommand(
            SourceWarehouseId: 1,
            TargetWarehouseId: 2,
            ItemId: 10,
            Quantity: 10.00m,
            TransactionDateUtc: DateTime.UtcNow,
            Description: "Test Transfer"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        
        // Stok Kontrolü
        var s1 = db.Stocks.Single(s => s.WarehouseId == 1);
        var s2 = db.Stocks.Single(s => s.WarehouseId == 2);
        
        Assert.Equal(90m, s1.Quantity);
        Assert.Equal(10m, s2.Quantity);
        
        // Hareket Kontrolü
        var movements = db.StockMovements.ToList();
        Assert.Equal(2, movements.Count);
        
        Assert.Contains(movements, m => m.Type == StockMovementType.TransferOut && m.Quantity == 10m && m.WarehouseId == 1);
        Assert.Contains(movements, m => m.Type == StockMovementType.TransferIn && m.Quantity == 10m && m.WarehouseId == 2);
    }

    [Fact]
    public async Task TransferStock_ShouldFail_WhenInsufficientStock()
    {
        // Arrange
        var db = GetDbContext();
        var rowVer = new byte[] { 1 };
        var branch = new Branch { Id = 1, Name = "Main Branch", Code = "MAIN", RowVersion = rowVer };
        var w1 = new Warehouse { Id = 1, BranchId = 1, Name = "Source", Code = "W1", RowVersion = rowVer };
        var w2 = new Warehouse { Id = 2, BranchId = 1, Name = "Target", Code = "W2", RowVersion = rowVer };
        var item = new Item { Id = 10, BranchId = 1, Name = "Item X", Code = "ITM01", RowVersion = rowVer };
        
        // 5 Adet Stok
        var stock1 = new Stock { BranchId = 1, WarehouseId = 1, ItemId = 10, Quantity = 5m, RowVersion = rowVer };

        db.Branches.Add(branch);
        db.Warehouses.AddRange(w1, w2);
        db.Items.Add(item);
        db.Stocks.Add(stock1);
        await db.SaveChangesAsync();

        var handler = new TransferStockHandler(db);
        var command = new TransferStockCommand(1, 2, 10, 10.000m, DateTime.UtcNow, "Overdraft");

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task TransferStock_ShouldFail_WhenCrossBranch()
    {
         // Arrange
        var db = GetDbContext();
        var rowVer = new byte[] { 1 };
        var b1 = new Branch { Id = 1, Name = "B1", Code = "B1", RowVersion = rowVer };
        var b2 = new Branch { Id = 2, Name = "B2", Code = "B2", RowVersion = rowVer };

        var w1 = new Warehouse { Id = 1, BranchId = 1, Name = "W1", Code = "W1", RowVersion = rowVer }; // Branch 1
        var w2 = new Warehouse { Id = 2, BranchId = 2, Name = "W2", Code = "W2", RowVersion = rowVer }; // Branch 2
        
        db.Branches.AddRange(b1, b2);
        db.Warehouses.AddRange(w1, w2);
        await db.SaveChangesAsync();

        var handler = new TransferStockHandler(db);
        var command = new TransferStockCommand(1, 2, 99, 10.000m, DateTime.UtcNow, "Cross Branch");

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(command, CancellationToken.None));
    }
}
