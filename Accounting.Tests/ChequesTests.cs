using Accounting.Application.Cheques.Commands.Create;
using Accounting.Application.Cheques.Commands.UpdateStatus;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Services;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Accounting.Tests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Accounting.Tests;

public class ChequesTests
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly Mock<IAccountBalanceService> _accountBalanceServiceMock;

    public ChequesTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _accountBalanceServiceMock = new Mock<IAccountBalanceService>();
    }

    [Fact]
    public async Task CreateCheque_ShouldSucceed()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Main Branch", Code = "BR-01" });
        await db.SaveChangesAsync();

        var handler = new CreateChequeHandler(db, userService);

        var command = new CreateChequeCommand(
            ContactId: null,
            Type: ChequeType.Cheque,
            Direction: ChequeDirection.Inbound,
            ChequeNumber: "CHQ-001",
            IssueDate: DateTime.UtcNow,
            DueDate: DateTime.UtcNow.AddDays(30),
            Amount: 10000m,
            Currency: "TRY",
            BankName: "TestBank",
            BankBranch: null,
            AccountNumber: null,
            DrawerName: "Drawer",
            Description: null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(0, result);
        var entity = await db.Cheques.FindAsync(result);
        Assert.NotNull(entity);
        Assert.Equal("CHQ-001", entity.ChequeNumber);
        Assert.Equal(ChequeStatus.Pending, entity.Status);
    }

    [Fact]
    public async Task UpdateChequeStatus_ToPaid_ShouldCreatePayment()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        // Seed
        db.Branches.Add(new Branch { Id = 1, Name = "Main Branch", Code = "BR-01" });
        var cheque = new Cheque
        {
            Id = 1,
            BranchId = 1,
            ChequeNumber = "CHQ-002",
            Status = ChequeStatus.Pending,
            Amount = 5000m,
            Currency = "TRY",
            Direction = ChequeDirection.Inbound,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow,
            RowVersion = Array.Empty<byte>()
        };
        db.Cheques.Add(cheque);
        await db.SaveChangesAsync();

        _accountBalanceServiceMock.Setup(x => x.RecalculateBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5000m);

        var handler = new UpdateChequeStatusHandler(db, _accountBalanceServiceMock.Object);

        var command = new UpdateChequeStatusCommand(
            Id: 1,
            NewStatus: ChequeStatus.Paid,
            Convert.ToBase64String(cheque.RowVersion),
            CashBankAccountId: 10 // Dummy Account
        );

        await handler.Handle(command, CancellationToken.None);

        var updatedCheque = await db.Cheques.FindAsync(1);
        Assert.Equal(ChequeStatus.Paid, updatedCheque.Status);

        var payment = await db.Payments.FirstOrDefaultAsync(p => p.ChequeId == 1);
        Assert.NotNull(payment);
        Assert.Equal(5000m, payment.Amount);
    }
}
