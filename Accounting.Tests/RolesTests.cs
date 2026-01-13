using Accounting.Application.Common.Interfaces;
using Accounting.Application.Roles.Commands.Create;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Accounting.Tests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Accounting.Tests;

public class RolesTests
{
    private readonly DbContextOptions<AppDbContext> _options;

    public RolesTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task CreateRole_ShouldSucceed()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        var handler = new CreateRoleHandler(db);

        var command = new CreateRoleCommand(
            Name: "Manager",
            Description: "Store Manager",
            Permissions: new List<string> { "invoice.create", "invoice.view" }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(0, result);
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == result);
        Assert.NotNull(role);
        Assert.Equal("Manager", role.Name);
        Assert.Equal(2, role.Permissions.Count);
    }
}
