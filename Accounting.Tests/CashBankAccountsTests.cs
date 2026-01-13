using Accounting.Application.CashBankAccounts.Commands.Create;
using Accounting.Application.CashBankAccounts.Commands.Update;
using Accounting.Application.CashBankAccounts.Commands.Delete;
using Accounting.Application.CashBankAccounts.Queries.GetById;
using Accounting.Application.CashBankAccounts.Queries.List;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums; // Add Enum namespace
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Accounting.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Accounting.Tests;

public class CashBankAccountsTests
{
    private readonly DbContextOptions<AppDbContext> _options;

    public CashBankAccountsTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task CreateCashBankAccount_ShouldSucceed()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        await db.SaveChangesAsync();

        // Handler sadece db alır
        var handler = new CreateCashBankAccountHandler(db);
        
        var command = new CreateCashBankAccountCommand(
            BranchId: 1,
            Type: CashBankAccountType.Cash,
            Name: "Main Cash",
            Iban: null
            // Diger alanlar yok (Code, Currency vs)
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(0, result.Id);
        var account = await db.CashBankAccounts.FindAsync(result.Id);
        Assert.NotNull(account);
        Assert.Equal("Main Cash", account.Name);
        Assert.Equal(CashBankAccountType.Cash, account.Type);
    }

    [Fact]
    public async Task UpdateCashBankAccount_ShouldSucceed()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        var account = new CashBankAccount
        {
            BranchId = 1,
            Name = "Old Name",
            Code = "ACC-001",
            Type = CashBankAccountType.Cash,
            Currency = "TRY", // Entity'de var
            RowVersion = Array.Empty<byte>()
        };
        db.CashBankAccounts.Add(account);
        await db.SaveChangesAsync();

        var handler = new UpdateCashBankAccountHandler(db, userService);
        
        var command = new UpdateCashBankAccountCommand(
            Id: account.Id,
            Type: CashBankAccountType.Bank,
            Name: "Updated Account",
            Iban: "TR123456789",
            RowVersion: Convert.ToBase64String(account.RowVersion)
            // Currency yok
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Updated Account", result.Name);
        Assert.Equal("Bank", result.Type); 
    }

    [Fact]
    public async Task SoftDeleteCashBankAccount_ShouldMarkAsDeleted()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        var account = new CashBankAccount
        {
            BranchId = 1,
            Name = "To Delete",
            Code = "DEL-001",
            Type = CashBankAccountType.Cash,
            Currency = "TRY",
            RowVersion = Array.Empty<byte>()
        };
        db.CashBankAccounts.Add(account);
        await db.SaveChangesAsync();

        var handler = new SoftDeleteCashBankAccountHandler(db, userService); // Add userService
        await handler.Handle(new SoftDeleteCashBankAccountCommand(account.Id, Convert.ToBase64String(account.RowVersion)), CancellationToken.None);

        var deletedAccount = await db.CashBankAccounts.IgnoreQueryFilters().FirstAsync(a => a.Id == account.Id);
        Assert.True(deletedAccount.IsDeleted);
    }

    [Fact]
    public async Task GetCashBankAccountById_ShouldReturnAccount()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        var account = new CashBankAccount
        {
            BranchId = 1,
            Name = "Test Account",
            Code = "TST-001",
            Type = CashBankAccountType.Cash,
            Currency = "TRY",
            RowVersion = Array.Empty<byte>()
        };
        db.CashBankAccounts.Add(account);
        await db.SaveChangesAsync();

        var handler = new GetCashBankAccountByIdHandler(db, userService); // Add userService
        var result = await handler.Handle(new GetCashBankAccountByIdQuery(account.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Test Account", result.Name);
    }

    [Fact]
    public async Task ListCashBankAccounts_ShouldReturnAll()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" });
        for (int i = 1; i <= 3; i++)
        {
            db.CashBankAccounts.Add(new CashBankAccount
            {
                BranchId = 1,
                Name = $"Account {i}",
                Code = $"ACC-{i:000}",
                Type = CashBankAccountType.Cash,
                Currency = "TRY",
                RowVersion = Array.Empty<byte>()
            });
        }
        await db.SaveChangesAsync();

        var handler = new ListCashBankAccountsHandler(db, userService);
        var result = await handler.Handle(new ListCashBankAccountsQuery(BranchId: 1), CancellationToken.None);

        Assert.Equal(3, result.Total); // Change from TotalCount to Total
    }
}
