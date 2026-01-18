using Accounting.Application.Categories.Commands.Create;
using Accounting.Application.Categories.Commands.Delete;
using Accounting.Application.Categories.Commands.Update;
using Accounting.Application.Categories.Queries.List;
using Accounting.Application.Common.Exceptions;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence;
using Accounting.Application.Categories.Commands.Create;
using Accounting.Application.Categories.Commands.Delete;
using Accounting.Application.Categories.Commands.Update;
using Accounting.Application.Categories.Queries.List;
using Accounting.Application.Common.Exceptions;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Accounting.Tests.Common;

namespace Accounting.Tests;

public class CategoryTests
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
    public async Task CreateCategory_ShouldSucceed()
    {
        var db = GetDbContext();
        var handler = new CreateCategoryHandler(db);
        var cmd = new CreateCategoryCommand("Electronics", "All gadgets", "#FF0000");

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.NotEqual(0, result.Id);
        Assert.Equal("Electronics", result.Name);
        Assert.NotNull(result.RowVersion);

        var inDb = await db.Categories.FindAsync(result.Id);
        Assert.NotNull(inDb);
        Assert.Equal("Electronics", inDb.Name);
    }

    [Fact]
    public async Task UpdateCategory_ShouldSucceed()
    {
        var db = GetDbContext();
        var category = new Category
        {
            Name = "Old Name",
            RowVersion = [1] // Dummy RowVersion for InMemory
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var handler = new UpdateCategoryHandler(db);
        var cmd = new UpdateCategoryCommand(
            category.Id,
            "New Name",
            "Desc",
            "#000",
            Convert.ToBase64String(category.RowVersion)
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("New Name", result.Name);

        var inDb = await db.Categories.FindAsync(category.Id);
        Assert.Equal("New Name", inDb!.Name);
    }

    [Fact]
    public async Task DeleteCategory_ShouldSucceed_WhenNotUsed()
    {
        var db = GetDbContext();
        var category = new Category { Name = "To Delete", RowVersion = [1] };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var handler = new DeleteCategoryHandler(db);
        var cmd = new DeleteCategoryCommand(category.Id, Convert.ToBase64String(category.RowVersion));

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result);
        var inDb = await db.Categories.FindAsync(category.Id);
        // Soft deleted?
        Assert.True(inDb!.IsDeleted);
    }

    [Fact]
    public async Task DeleteCategory_ShouldFail_WhenUsedByItem()
    {
        var db = GetDbContext();
        var rowVer = new byte[] { 1 };
        var branch = new Branch { Id = 1, Name = "B1", Code = "B1", RowVersion = rowVer };
        var category = new Category { Id = 10, Name = "Used Cat", RowVersion = rowVer };
        var item = new Item
        {
            CategoryId = 10,
            Name = "Item X",
            Code = "X",
            RowVersion = rowVer
        };

        db.Branches.Add(branch);
        db.Categories.Add(category);
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var handler = new DeleteCategoryHandler(db);
        var cmd = new DeleteCategoryCommand(category.Id, Convert.ToBase64String(category.RowVersion));

        await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task GetCategories_ShouldListAllalphabetically()
    {
        var db = GetDbContext();
        db.Categories.AddRange(
            new Category { Name = "Zebra", RowVersion = [1] },
            new Category { Name = "Alpha", RowVersion = [1] },
            new Category { Name = "Beta", RowVersion = [1] }
        );
        await db.SaveChangesAsync();

        var handler = new ListCategoriesHandler(db);
        var result = await handler.Handle(new ListCategoriesQuery(), CancellationToken.None);

        Assert.Equal(3, result.Total);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal("Alpha", result.Items[0].Name);
        Assert.Equal("Beta", result.Items[1].Name);
        Assert.Equal("Zebra", result.Items[2].Name);
    }
}