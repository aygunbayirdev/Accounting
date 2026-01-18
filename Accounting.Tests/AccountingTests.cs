using Accounting.Application.Cheques.Commands.Create;
using Accounting.Application.Cheques.Commands.UpdateStatus;
using Accounting.Application.Items.Commands.Create;
using Accounting.Application.Services;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Accounting.Application.Common.Abstractions;
using Moq;
using Accounting.Tests.Common;

namespace Accounting.Tests
{
    public class AccountingTests
    {
        private DbContextOptions<AppDbContext> _options;

        public AccountingTests()
        {
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        [Fact]
        public async Task Handle_ShouldCreateInboundCheque()
        {
            var userService = new FakeCurrentUserService(1);
            var audit = new AuditSaveChangesInterceptor(userService);
            using (var db = new AppDbContext(_options, audit, userService))
            {
                // Seed Dependencies
                var branch = new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" };
                db.Branches.Add(branch);

                var contact = new Contact { Id = 1, Name = "Test Customer", Code = "CUS-001", IsCustomer = true, BranchId = 1, RowVersion = Array.Empty<byte>() };
                db.Contacts.Add(contact);

                await db.SaveChangesAsync();

                var handler = new CreateChequeHandler(db, new FakeCurrentUserService(1));

                var cmd = new CreateChequeCommand(
                    ContactId: 1,
                    Type: ChequeType.Cheque,
                    Direction: ChequeDirection.Inbound,
                    ChequeNumber: "FINAL-001",
                    IssueDate: DateTime.UtcNow,
                    DueDate: DateTime.UtcNow.AddDays(30),
                    Amount: 1000.00m,
                    Currency: "TRY",
                    BankName: "Test Bank",
                    BankBranch: "Main",
                    AccountNumber: "123456",
                    DrawerName: "Drawer",
                    Description: "Test Cheque"
                );

                var resultId = await handler.Handle(cmd, CancellationToken.None);

                Assert.NotEqual(0, resultId);

                var cheque = await db.Cheques.FindAsync(resultId);
                Assert.NotNull(cheque);
                Assert.Equal("FINAL-001", cheque.ChequeNumber);
            }
        }

        [Fact]
        public async Task Handle_ShouldUpdateStatusAndCreatePayment_WhenPaid()
        {
            var userService = new FakeCurrentUserService(1);
            var audit = new AuditSaveChangesInterceptor(userService);
            using (var db = new AppDbContext(_options, audit, userService))
            {
                // Seed Dependencies
                var branch = new Branch { Id = 1, Name = "Test Branch", Code = "BR-01" };
                db.Branches.Add(branch);

                var contact = new Contact { Id = 5, Name = "Test Vendor", Code = "VEN-005", IsVendor = true, BranchId = 1, RowVersion = Array.Empty<byte>() };
                db.Contacts.Add(contact);

                var account = new CashBankAccount { Id = 10, Name = "Main Cash", Code = "CASH-01", Currency = "TRY", BranchId = 1, RowVersion = Array.Empty<byte>() };
                db.CashBankAccounts.Add(account);

                var cheque = new Cheque
                {
                    BranchId = 1,
                    ContactId = 5,
                    Type = ChequeType.Cheque,
                    Direction = ChequeDirection.Inbound,
                    Status = ChequeStatus.Pending,
                    ChequeNumber = "PAY-test-002",
                    Amount = 2500m,
                    Currency = "TRY",
                    DueDate = DateTime.UtcNow,
                    RowVersion = Array.Empty<byte>()
                };
                db.Cheques.Add(cheque);
                await db.SaveChangesAsync();

                var absMock = new Mock<IAccountBalanceService>();
                var handler = new UpdateChequeStatusHandler(db, absMock.Object);
                var cmd = new UpdateChequeStatusCommand(cheque.Id, ChequeStatus.Paid, Convert.ToBase64String(cheque.RowVersion), CashBankAccountId: 10);

                await handler.Handle(cmd, CancellationToken.None);

                var updated = await db.Cheques.FindAsync(cheque.Id);
                Assert.Equal(ChequeStatus.Paid, updated.Status);

                var payment = await db.Payments.FirstOrDefaultAsync(p => p.ChequeId == cheque.Id);
                Assert.NotNull(payment);
                Assert.Equal(2500m, payment.Amount);
            }
        }

        [Fact]
        public async Task Handle_ShouldCreateItemWithSeparatePrices()
        {
            var userService = new FakeCurrentUserService(1);
            var audit = new AuditSaveChangesInterceptor(userService);
            using (var db = new AppDbContext(_options, audit, userService))
            {
                var handler = new CreateItemHandler(db, new FakeCurrentUserService(1));

                var cmd = new CreateItemCommand(
                    CategoryId: null,
                    Code: "ITM-001",
                    Name: "Dual Price Product",
                    Type: 1, // Inventory
                    Unit: "Pcs",
                    VatRate: 18,
                    DefaultWithholdingRate: null,
                    PurchasePrice: 85.50m,
                    SalesPrice: 120.00m,
                    null,
                    null,
                    null
                );

                var result = await handler.Handle(cmd, CancellationToken.None);

                Assert.NotEqual(0, result.Id);

                var item = await db.Items.FindAsync(result.Id);
                Assert.NotNull(item);
                Assert.Equal("Dual Price Product", item.Name);
                Assert.Equal(85.50m, item.PurchasePrice);
                Assert.Equal(120.00m, item.SalesPrice);
            }
        }
    }
}
