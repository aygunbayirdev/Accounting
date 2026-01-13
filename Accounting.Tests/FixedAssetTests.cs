using Accounting.Application.Common.Interfaces;
using Accounting.Application.FixedAssets.Commands.Create;
using Accounting.Application.FixedAssets.Commands.Update;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Accounting.Tests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Accounting.Tests;

public class FixedAssetTests
{
    private readonly DbContextOptions<AppDbContext> _options;

    public FixedAssetTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task CreateFixedAsset_ShouldSucceed_WhenValid()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        // Seed
        db.Branches.Add(new Branch { Id = 1, Name = "Main Branch", Code = "BR-01" });
        await db.SaveChangesAsync();

        var handler = new CreateFixedAssetHandler(db);

        var command = new CreateFixedAssetCommand(
            BranchId: 1,
            Code: "FA-001",
            Name: "Laptop",
            PurchaseDateUtc: DateTime.UtcNow,
            PurchasePrice: 15000m,
            UsefulLifeYears: 5
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("FA-001", result.Code);
        Assert.Equal(20m, result.DepreciationRatePercent); // 100 / 5 = 20
    }

    [Fact]
    public async Task UpdateFixedAsset_ShouldSucceed()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        // Seed
        db.Branches.Add(new Branch { Id = 1, Name = "Main Branch", Code = "BR-01" });
        var asset = new FixedAsset { 
            Id = 1, 
            BranchId = 1, 
            Code = "FA-001", 
            Name = "Laptop", 
            PurchaseDateUtc = DateTime.UtcNow, 
            PurchasePrice = 15000m, 
            UsefulLifeYears = 5,
            DepreciationRatePercent = 20m,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        db.FixedAssets.Add(asset);
        await db.SaveChangesAsync();

        var handler = new UpdateFixedAssetHandler(db, userService);

        var command = new UpdateFixedAssetCommand(
            Id: 1,
            RowVersionBase64: Convert.ToBase64String(asset.RowVersion),
            Code: "FA-001-UPD",
            Name: "Laptop Pro",
            PurchaseDateUtc: DateTime.UtcNow,
            PurchasePrice: 20000m,
            UsefulLifeYears: 4
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("FA-001-UPD", result.Code);
        Assert.Equal(25m, result.DepreciationRatePercent); // 100 / 4 = 25
    }
}
