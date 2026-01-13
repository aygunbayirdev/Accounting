using Accounting.Application.Items.Commands.Create;
using Accounting.Application.Items.Commands.Update;
using Accounting.Application.Items.Commands.Delete;
using Accounting.Application.Items.Queries.GetById;
using Accounting.Application.Items.Queries.List;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Accounting.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Accounting.Tests;

/// <summary>
/// Items (Stok/Hizmet Kartları) CRUD testleri
/// 
/// TEST YAZMA REHBERİ:
/// 1. ARRANGE (Hazırlık): Fake services ve test data'yı hazırla
/// 2. ACT (İşlem): Test edilecek kodu çalıştır (Handler.Handle)
/// 3. ASSERT (Doğrulama): Sonucu kontrol et (Assert.Equal, Assert.NotNull, vb.)
/// </summary>
public class ItemsTests
{
    private readonly DbContextOptions<AppDbContext> _options;

    public ItemsTests()
    {
        // Her test için temiz bir InMemory database oluştur
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    #region Create Tests

    [Fact]
    public async Task CreateItem_ShouldSucceed_WhenValidData()
    {
        // ARRANGE - Hazırlık (Test ortamını kur)
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        
        using var db = new AppDbContext(_options, audit, userService);

        // Branch seed (Item BranchId gerektirir)
        db.Branches.Add(new Branch { Id = 1, Name = "Ana Şube", Code = "BR-01" });
        await db.SaveChangesAsync();

        var handler = new CreateItemHandler(db, userService);

        // Command imzası: CategoryId, Code, Name, Unit, VatRate, PurchasePrice, SalesPrice
        var command = new CreateItemCommand(
            CategoryId: null,
            Code: "IT-001",
            Name: "Laptop",
            Unit: "adet",
            VatRate: 20,
            PurchasePrice: "5000.00",
            SalesPrice: "7000.00"
        );

        // ACT - İşlem (Test edilecek kodu çalıştır)
        var result = await handler.Handle(command, CancellationToken.None);

        // ASSERT - Doğrulama (Sonucu kontrol et)
        Assert.NotEqual(0, result.Id); // ID üretilmiş mi?
        
        var item = await db.Items.FindAsync(result.Id);
        Assert.NotNull(item);
        Assert.Equal("Laptop", item.Name);
        Assert.Equal("IT-001", item.Code);
        Assert.Equal(5000m, item.PurchasePrice);
        Assert.Equal(7000m, item.SalesPrice);
        Assert.Equal(1, item.BranchId); // CurrentUser'dan geldi
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateItem_ShouldSucceed_WhenValidData()
    {
        // ARRANGE
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        
        using var db = new AppDbContext(_options, audit, userService);
        db.Branches.Add(new Branch { Id = 1, Name = "Ana Şube", Code = "BR-01" });

        var item = new Item
        {
            BranchId = 1,
            Name = "Old Name",
            Code = "IT-002",
            Type = ItemType.Inventory,
            Unit = "adet",
            VatRate = 20,
            SalesPrice = 1000m,
            RowVersion = Array.Empty<byte>()
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var handler = new UpdateItemHandler(db, userService);

        // UpdateItemCommand imzası: Id, CategoryId, Name, Unit, VatRate, PurchasePrice, SalesPrice, RowVersion
        var command = new UpdateItemCommand(
            Id: item.Id,
            CategoryId: null,
            Name: "Updated Name",
            Unit: "kutu",
            VatRate: 18,
            PurchasePrice: "800.00",
            SalesPrice: "1200.00",
            RowVersion: Convert.ToBase64String(item.RowVersion)
        );

        // ACT
        var result = await handler.Handle(command, CancellationToken.None);

        // ASSERT
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal("kutu", result.Unit);
        Assert.Equal(18, result.VatRate);
        Assert.Equal("1200.00", result.SalesPrice);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task SoftDeleteItem_ShouldMarkAsDeleted()
    {
        // ARRANGE
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        
        using var db = new AppDbContext(_options, audit, userService);
        db.Branches.Add(new Branch { Id = 1, Name = "Ana Şube", Code = "BR-01" });

        var item = new Item
        {
            BranchId = 1,
            Name = "To Delete",
            Code = "DEL-001",
            Type = ItemType.Inventory,
            Unit = "adet",
            VatRate = 20,
            RowVersion = Array.Empty<byte>()
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var handler = new SoftDeleteItemHandler(db, userService);
        var command = new SoftDeleteItemCommand(item.Id, Convert.ToBase64String(item.RowVersion));

        // ACT
        await handler.Handle(command, CancellationToken.None);

        // ASSERT
        var deletedItem = await db.Items.IgnoreQueryFilters().FirstAsync(i => i.Id == item.Id);
        Assert.True(deletedItem.IsDeleted); // Soft delete ile IsDeleted = true
        Assert.NotNull(deletedItem.DeletedAtUtc);

        // Global query filter aktif olunca bulunamaz
        var notFoundItem = await db.Items.FirstOrDefaultAsync(i => i.Id == item.Id);
        Assert.Null(notFoundItem);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetItemById_ShouldReturnItem_WhenExists()
    {
        // ARRANGE
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        
        using var db = new AppDbContext(_options, audit, userService);
        db.Branches.Add(new Branch { Id = 1, Name = "Ana Şube", Code = "BR-01" });

        var item = new Item
        {
            BranchId = 1,
            Name = "Test Item",
            Code = "TST-001",
            Type = ItemType.Inventory,
            Unit = "adet",
            VatRate = 20,
            SalesPrice = 100m,
            RowVersion = Array.Empty<byte>()
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var handler = new GetItemByIdHandler(db, userService);

        // ACT
        var result = await handler.Handle(new GetItemByIdQuery(item.Id), CancellationToken.None);

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal("Test Item", result.Name);
    }

    [Fact]
    public async Task ListItems_ShouldReturnPagedResults()
    {
        // ARRANGE
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        
        using var db = new AppDbContext(_options, audit, userService);
        db.Branches.Add(new Branch { Id = 1, Name = "Ana Şube", Code = "BR-01" });

        // 5 item ekle
        for (int i = 1; i <= 5; i++)
        {
            db.Items.Add(new Item
            {
                BranchId = 1,
                Name = $"Item {i}",
                Code = $"IT-{i:000}",
                Type = ItemType.Inventory,
                Unit = "adet",
                VatRate = 20,
                RowVersion = Array.Empty<byte>()
            });
        }
        await db.SaveChangesAsync();

        var handler = new ListItemsHandler(db, userService);
        
        // ListItemsQuery imzası kontrol et
        var query = new ListItemsQuery(
            PageNumber: 1, 
            PageSize: 3, 
            Sort: "name:asc", 
            Search: null, 
            CategoryId: null
        );

        // ACT
        var result = await handler.Handle(query, CancellationToken.None);

        // ASSERT
        Assert.Equal(5, result.Total); // PagedResult.Total kullan
        Assert.Equal(3, result.Items.Count); // Sayfa başına 3
    }

    #endregion

    #region Business Rule Tests

    [Fact]
    public async Task CreateItem_ShouldFail_WhenDuplicateCode()
    {
        // ARRANGE
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        
        using var db = new AppDbContext(_options, audit, userService);
        db.Branches.Add(new Branch { Id = 1, Name = "Ana Şube", Code = "BR-01" });

        // Önce bir item oluştur
        db.Items.Add(new Item
        {
            BranchId = 1,
            Name = "First Item",
            Code = "DUPLICATE",
            Type = ItemType.Inventory,
            Unit = "adet",
            VatRate = 20,
            RowVersion = Array.Empty<byte>()
        });
        await db.SaveChangesAsync();

        var handler = new CreateItemHandler(db, userService);
        var command = new CreateItemCommand(
            CategoryId: null,
            Code: "DUPLICATE", // Aynı kod
            Name: "Second Item",
            Unit: "adet",
            VatRate: 20,
            PurchasePrice: null,
            SalesPrice: null
        );

        // ACT & ASSERT - Exception bekliyoruz
        await Assert.ThrowsAsync<Accounting.Application.Common.Exceptions.BusinessRuleException>(
            async () => await handler.Handle(command, CancellationToken.None)
        );
    }

    #endregion
}
