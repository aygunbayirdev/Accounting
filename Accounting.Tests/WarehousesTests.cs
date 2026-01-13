using Accounting.Application.Warehouses.Commands.Create;
using Accounting.Application.Warehouses.Commands.Update;
using Accounting.Application.Warehouses.Commands.Delete;
using Accounting.Application.Warehouses.Queries.GetById;
using Accounting.Application.Warehouses.Queries.List;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Accounting.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Accounting.Tests;

public class WarehousesTests
{
    private readonly DbContextOptions<AppDbContext> _options;

    public WarehousesTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task CreateWarehouse_ShouldSucceed()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        await db.SaveChangesAsync();

        var handler = new CreateWarehouseHandler(db, userService);
        
        // Command'de BranchId YOK, user service'den alıyor
        var command = new CreateWarehouseCommand(
            Code: "WH-001",
            Name: "Main Warehouse",
            IsDefault: true
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(0, result.Id);
        var warehouse = await db.Warehouses.FindAsync(result.Id);
        Assert.NotNull(warehouse);
        Assert.Equal("Main Warehouse", warehouse.Name);
        Assert.True(warehouse.IsDefault);
        Assert.Equal(1, warehouse.BranchId);
    }

    [Fact]
    public async Task UpdateWarehouse_ShouldSucceed()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        var warehouse = new Warehouse
        {
            BranchId = 1,
            Name = "Old Name",
            Code = "WH-001",
            IsDefault = false,
            RowVersion = Array.Empty<byte>()
        };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();

        var handler = new UpdateWarehouseHandler(db, userService);
        
        // Update command BranchId istiyor
        var command = new UpdateWarehouseCommand(
            Id: warehouse.Id,
            BranchId: 1, 
            Code: "WH-001",
            Name: "Updated Warehouse",
            IsDefault: true,
            RowVersion: Convert.ToBase64String(warehouse.RowVersion)
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Updated Warehouse", result.Name);
        Assert.True(result.IsDefault);
    }

    [Fact]
    public async Task SoftDeleteWarehouse_ShouldMarkAsDeleted()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        var warehouse = new Warehouse
        {
            BranchId = 1,
            Name = "To Delete",
            Code = "DEL-001",
            IsDefault = false,
            RowVersion = Array.Empty<byte>()
        };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();

        var handler = new SoftDeleteWarehouseHandler(db, userService);
        await handler.Handle(new SoftDeleteWarehouseCommand(warehouse.Id, Convert.ToBase64String(warehouse.RowVersion)), CancellationToken.None);

        var deletedWarehouse = await db.Warehouses.IgnoreQueryFilters().FirstAsync(w => w.Id == warehouse.Id);
        Assert.True(deletedWarehouse.IsDeleted);
    }

    [Fact]
    public async Task GetWarehouseById_ShouldReturnWarehouse()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        var warehouse = new Warehouse
        {
            BranchId = 1,
            Name = "Test Warehouse",
            Code = "TST-001",
            IsDefault = false,
            RowVersion = Array.Empty<byte>()
        };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();

        var handler = new GetWarehouseByIdHandler(db, userService);
        var result = await handler.Handle(new GetWarehouseByIdQuery(warehouse.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Test Warehouse", result.Name);
    }

    [Fact]
    public async Task ListWarehouses_ShouldReturnAll()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        for (int i = 1; i <= 3; i++)
        {
            db.Warehouses.Add(new Warehouse
            {
                BranchId = 1,
                Name = $"Warehouse {i}",
                Code = $"WH-{i:000}",
                IsDefault = i == 1,
                RowVersion = Array.Empty<byte>()
            });
        }
        await db.SaveChangesAsync();

        var handler = new ListWarehousesHandler(db, userService);
        
        // List query BranchId istiyor
        var result = await handler.Handle(new ListWarehousesQuery(BranchId: 1), CancellationToken.None);

        Assert.Equal(3, result.Total);
    }
}
