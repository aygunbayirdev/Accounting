using Accounting.Application.Common.Interfaces;
using Accounting.Application.Payments.Commands.Create;
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

public class PaymentsTests
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly Mock<IInvoiceBalanceService> _invoiceBalanceServiceMock;
    private readonly Mock<IAccountBalanceService> _accountBalanceServiceMock;

    public PaymentsTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _invoiceBalanceServiceMock = new Mock<IInvoiceBalanceService>();
        _accountBalanceServiceMock = new Mock<IAccountBalanceService>();
    }

    [Fact]
    public async Task CreatePayment_ShouldSucceed_WhenValidData()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        // Seed
        db.Branches.Add(new Branch { Id = 1, Name = "Main Branch", Code = "BR-01" });
        db.CashBankAccounts.Add(new CashBankAccount { Id = 10, BranchId = 1, Name = "Main Cash", Code = "CASH-01", Type = CashBankAccountType.Cash, Currency = "TRY" });
        await db.SaveChangesAsync();

        // Mocks
        _accountBalanceServiceMock.Setup(x => x.RecalculateBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(100m);
        _invoiceBalanceServiceMock.Setup(x => x.RecalculateBalanceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0m);

        var handler = new CreatePaymentHandler(db, _invoiceBalanceServiceMock.Object, _accountBalanceServiceMock.Object, userService);

        var command = new CreatePaymentCommand(
            AccountId: 10,
            ContactId: null,
            LinkedInvoiceId: null,
            DateUtc: DateTime.UtcNow.ToString("O"),
            Direction: PaymentDirection.In,
            Amount: "100.50",
            Currency: "TRY",
            Description: null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(0, result.Id);
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == result.Id);
        Assert.NotNull(payment);
        Assert.Equal(100.50m, payment.Amount);
    }
}
