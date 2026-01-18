using Accounting.Application.Branches.Commands.Create;
using Accounting.Application.Branches.Commands.Delete;
using Accounting.Application.Branches.Commands.Update;
using Accounting.Application.Branches.Queries.GetById;
using Accounting.Application.Common.Exceptions;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Accounting.Application.Branches.Commands.Create;
using Accounting.Application.Branches.Commands.Delete;
using Accounting.Application.Branches.Commands.Update;
using Accounting.Application.Branches.Queries.GetById;
using Accounting.Application.Common.Exceptions;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Accounting.Tests.Common;

namespace Accounting.Tests;

public class BranchTests
{
    private readonly DbContextOptions<AppDbContext> _options;

    public BranchTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task Create_ShouldCreateBranch_WhenValid()
    {
        var userService = new FakeCurrentUserService(1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using (var db = new AppDbContext(_options, audit, userService))
        {
            var handler = new CreateBranchHandler(db);
            var cmd = new CreateBranchCommand("TEST-CRE", "Test Branch");

            var result = await handler.Handle(cmd, CancellationToken.None);

            Assert.Equal("TEST-CRE", result.Code);
            Assert.Equal("Test Branch", result.Name);
            Assert.NotNull(result.RowVersion);

            var inDb = await db.Branches.FindAsync(result.Id);
            Assert.NotNull(inDb);
        }
    }

    [Fact]
    public async Task Create_ShouldFailValidation_WhenCodeExists()
    {
        var userService = new FakeCurrentUserService(1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using (var db = new AppDbContext(_options, audit, userService))
        {
            db.Branches.Add(new Branch { Code = "DUPLICATE", Name = "Existing", RowVersion = new byte[] { 1 } });
            await db.SaveChangesAsync();

            var validator = new CreateBranchValidator(db);
            var cmd = new CreateBranchCommand("DUPLICATE", "New Branch");

            var result = await validator.ValidateAsync(cmd);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, x => x.PropertyName == "Code");
        }
    }

    [Fact]
    public async Task Update_ShouldUpdateBranch_WhenValid()
    {
        var userService = new FakeCurrentUserService(1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using (var db = new AppDbContext(_options, audit, userService))
        {
            var entity = new Branch { Code = "OLD", Name = "Old Name", RowVersion = new byte[] { 1 } };
            db.Branches.Add(entity);
            await db.SaveChangesAsync();

            var handler = new UpdateBranchHandler(db);
            var rv = Convert.ToBase64String(entity.RowVersion);
            var cmd = new UpdateBranchCommand(entity.Id, "NEW", "New Name", rv);

            var result = await handler.Handle(cmd, CancellationToken.None);

            Assert.Equal("NEW", result.Code);
            Assert.Equal("New Name", result.Name);

            var inDb = await db.Branches.FindAsync(entity.Id);
            Assert.Equal("NEW", inDb.Code);
        }
    }

    [Fact]
    public async Task Update_ShouldThrowConcurrency_WhenVersionMismatch()
    {
        var userService = new FakeCurrentUserService(1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using (var db = new AppDbContext(_options, audit, userService))
        {
            var entity = new Branch { Code = "CONC", Name = "C", RowVersion = new byte[] { 1 } };
            db.Branches.Add(entity);
            await db.SaveChangesAsync();

            var handler = new UpdateBranchHandler(db);
            var rv = Convert.ToBase64String(new byte[] { 2 }); // Wrong version
            var cmd = new UpdateBranchCommand(entity.Id, "UPD", "U", rv);

            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => handler.Handle(cmd, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Delete_ShouldSoftDelete()
    {
        var userService = new FakeCurrentUserService(1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using (var db = new AppDbContext(_options, audit, userService))
        {
            var entity = new Branch { Code = "DEL", Name = "Delete Me", RowVersion = new byte[] { 1 } };
            db.Branches.Add(entity);
            await db.SaveChangesAsync();

            var handler = new DeleteBranchHandler(db);
            var rv = Convert.ToBase64String(entity.RowVersion);
            var cmd = new DeleteBranchCommand(entity.Id, rv);

            await handler.Handle(cmd, CancellationToken.None);

            var deleted = await db.Branches.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == entity.Id);
            Assert.True(deleted.IsDeleted);
            Assert.NotNull(deleted.DeletedAtUtc);

            // Verify GetById fails (Global Filter check logic)
            // Note: Since GetById is in a separate context usually, we simulate it here
            var getHandler = new GetBranchByIdHandler(db);
            await Assert.ThrowsAsync<NotFoundException>(() => getHandler.Handle(new GetBranchByIdQuery(entity.Id), CancellationToken.None));
        }
    }
}
