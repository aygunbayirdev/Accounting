using Accounting.Application.Common.Interfaces;
using Accounting.Application.ExpenseDefinitions.Commands.Create;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Accounting.Tests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Accounting.Tests;

public class ExpenseDefinitionTests
{
    private readonly DbContextOptions<AppDbContext> _options;

    public ExpenseDefinitionTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task CreateExpenseDefinition_ShouldSucceed()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        // Seed Branch (Required for validation if any, primarily for multitenancy)
        db.Branches.Add(new Branch { Id = 1, Name = "Main Branch", Code = "BR-01" });
        await db.SaveChangesAsync();

        var handler = new CreateExpenseDefinitionHandler(db, userService);

        var command = new CreateExpenseDefinitionCommand(
            Code: "EXP-01",
            Name: "Office Supplies"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(0, result);
        var entity = await db.ExpenseDefinitions.FindAsync(result);
        Assert.NotNull(entity);
        Assert.Equal("Office Supplies", entity.Name);
        Assert.Equal("EXP-01", entity.Code);
    }
}
