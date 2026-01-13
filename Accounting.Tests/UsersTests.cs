using Accounting.Application.Common.Interfaces;
using Accounting.Application.Users.Commands.Create;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Accounting.Tests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Accounting.Tests;

public class UsersTests
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;

    public UsersTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _passwordHasherMock = new Mock<IPasswordHasher>();
    }

    [Fact]
    public async Task CreateUser_ShouldSucceed_WhenValid()
    {
        var userService = new FakeCurrentUserService(branchId: 1); // Not really used but good practice
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        // Seed Branch and Roles
        db.Branches.Add(new Branch { Id = 1, Name = "Main Branch", Code = "BR-01" });
        db.Roles.Add(new Role { Id = 1, Name = "Admin" });
        db.Roles.Add(new Role { Id = 2, Name = "User" });
        await db.SaveChangesAsync();

        _passwordHasherMock.Setup(x => x.HashPassword(It.IsAny<string>())).Returns("hashed_secret");

        var handler = new CreateUserHandler(db, _passwordHasherMock.Object);

        var command = new CreateUserCommand(
            FirstName: "John",
            LastName: "Doe",
            Email: "john.doe@example.com",
            Password: "secret",
            BranchId: 1,
            IsActive: true,
            RoleIds: new List<int> { 1, 2 }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(0, result);
        var user = await db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == result);
        Assert.NotNull(user);
        Assert.Equal("John", user.FirstName);
        Assert.Equal("hashed_secret", user.PasswordHash);
        Assert.Equal(2, user.UserRoles.Count);
    }
}
