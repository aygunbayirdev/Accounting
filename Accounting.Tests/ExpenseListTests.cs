using Accounting.Application.Common.Interfaces;
using Accounting.Application.ExpenseLists.Commands.Create;
using Accounting.Application.ExpenseLists.Commands.PostToBill;
using Accounting.Application.Invoices.Commands.Create;
using Accounting.Application.Payments.Commands.Create;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Accounting.Application.Invoices.Queries.Dto;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Accounting.Application.Common.Exceptions;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Accounting.Tests;

public class ExpenseListTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly AppDbContext _db;

    public ExpenseListTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _mediatorMock = new Mock<IMediator>();

        _currentUserServiceMock.Setup(x => x.BranchId).Returns(1);
        _currentUserServiceMock.Setup(x => x.UserId).Returns(1);

        // AuditSaveChangesInterceptor uses DateTime.UtcNow internally, and takes ICurrentUserService in ctor
        var audit = new AuditSaveChangesInterceptor(_currentUserServiceMock.Object);
        
        _db = new AppDbContext(options, audit, _currentUserServiceMock.Object);

        // Seed common data
        _db.Branches.Add(new Branch { Id = 1, Name = "Main", Code = "B1" });
        _db.SaveChanges();
    }

    private CreateInvoiceResult CreateDummyInvoiceResult(int id)
    {
        return new CreateInvoiceResult(
            Id: id,
            TotalNet: "100.00",
            TotalVat: "18.00",
            TotalGross: "118.00",
            RoundingPolicy: "None"
        );
    }

    [Fact]
    public async Task CreateExpenseList_ShouldSucceed()
    {
        var handler = new CreateExpenseListHandler(_db);

        var command = new CreateExpenseListCommand(
            BranchId: 1,
            Name: "January Expenses",
            Lines: new List<Accounting.Application.ExpenseLists.Commands.Create.CreateExpenseLineDto>
            {
                new Accounting.Application.ExpenseLists.Commands.Create.CreateExpenseLineDto(
                    "2024-01-01T10:00:00Z", 
                    10, 
                    "USD", 
                    "100.00", 
                    18, 
                    "Food", 
                    "Lunch"),
                new Accounting.Application.ExpenseLists.Commands.Create.CreateExpenseLineDto(
                    "2024-01-02T10:00:00Z", 
                    10, 
                    "USD", 
                    "200.00", 
                    18, 
                    "Travel", 
                    "Taxi")
            }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("January Expenses", result.Name);
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal("Draft", result.Status);
        
        var dbList = await _db.ExpenseLists.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == result.Id);
        Assert.NotNull(dbList);
        Assert.Equal(2, dbList.Lines.Count);
    }

    [Fact]
    public async Task PostExpenseListToBill_ShouldSucceed()
    {
        // Arrange
        var expenseList = new ExpenseList
        {
            BranchId = 1,
            Name = "Approved Expenses",
            Status = ExpenseListStatus.Reviewed, // Must be reviewed
            RowVersion = Array.Empty<byte>(),
            CreatedAtUtc = DateTime.UtcNow
        };
        expenseList.Lines.Add(new ExpenseLine 
        { 
            Currency = "USD", 
            Amount = 100, 
            SupplierId = 5, 
            VatRate = 18,
            CreatedAtUtc = DateTime.UtcNow,
            DateUtc = DateTime.UtcNow,
            RowVersion = Array.Empty<byte>()
        });

        _db.ExpenseLists.Add(expenseList);
        await _db.SaveChangesAsync(); // Generates ID

        Assert.True(expenseList.Id > 0, "ExpenseList ID should be generated");
        Assert.Equal(1, expenseList.BranchId);

        var handler = new PostExpenseListToBillHandler(_db, _mediatorMock.Object, _currentUserServiceMock.Object);

        var command = new PostExpenseListToBillCommand(
            ExpenseListId: expenseList.Id,
            SupplierId: 5,
            ItemId: 99,
            Currency: "USD",
            CreatePayment: false,
            PaymentAccountId: null,
            PaymentDateUtc: null,
            DateUtc: "2024-01-05T10:00:00Z"
        );

        // Mock CreateInvoiceCommand response to return CreateInvoiceResult
        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateInvoiceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDummyInvoiceResult(1001));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1001, result.CreatedInvoiceId);
        
        var dbList = await _db.ExpenseLists.FindAsync(expenseList.Id);
        Assert.Equal(ExpenseListStatus.Posted, dbList.Status);
        Assert.Equal(1001, dbList.PostedInvoiceId);

        _mediatorMock.Verify(m => m.Send(It.IsAny<CreateInvoiceCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PostExpenseListToBill_ShouldFail_WhenStatusNotReviewed()
    {
        // Arrange
        var expenseList = new ExpenseList
        {
            BranchId = 1,
            Name = "Draft Expenses",
            Status = ExpenseListStatus.Draft, // Not Reviewed
            RowVersion = Array.Empty<byte>()
        };
        _db.ExpenseLists.Add(expenseList);
        await _db.SaveChangesAsync();

        var handler = new PostExpenseListToBillHandler(_db, _mediatorMock.Object, _currentUserServiceMock.Object);
        // Correct args using named args for clarity or positional if correct
        var command = new PostExpenseListToBillCommand(
            ExpenseListId: expenseList.Id, 
            SupplierId: 1, 
            ItemId: 99, 
            Currency: "USD", 
            CreatePayment: false
        );

        // Act & Assert
        await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task PostExpenseListToBill_ShouldFail_WhenCurrenciesDontMatch()
    {
        // Arrange
        var expenseList = new ExpenseList
        {
            BranchId = 1,
            Status = ExpenseListStatus.Reviewed,
            RowVersion = Array.Empty<byte>()
        };
        expenseList.Lines.Add(new ExpenseLine { Currency = "EUR", Amount = 100, SupplierId = 1, VatRate = 18, CreatedAtUtc = DateTime.UtcNow, DateUtc = DateTime.UtcNow });

        _db.ExpenseLists.Add(expenseList);
        await _db.SaveChangesAsync();

        var handler = new PostExpenseListToBillHandler(_db, _mediatorMock.Object, _currentUserServiceMock.Object);
        var command = new PostExpenseListToBillCommand(
            ExpenseListId: expenseList.Id, 
            SupplierId: 1, 
            ItemId: 99, 
            Currency: "USD", // Requesting USD but list has EUR
            CreatePayment: false
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }
    
    [Fact]
    public async Task PostExpenseListToBill_ShouldCreatePayment_WhenRequested()
    {
        // Arrange
        var expenseList = new ExpenseList
        {
            BranchId = 1,
            Status = ExpenseListStatus.Reviewed,
            RowVersion = Array.Empty<byte>()
        };
        expenseList.Lines.Add(new ExpenseLine { Currency = "USD", Amount = 100, SupplierId = 5, VatRate = 18, CreatedAtUtc = DateTime.UtcNow, DateUtc = DateTime.UtcNow });

        _db.ExpenseLists.Add(expenseList);
        await _db.SaveChangesAsync();

        var handler = new PostExpenseListToBillHandler(_db, _mediatorMock.Object, _currentUserServiceMock.Object);

        var command = new PostExpenseListToBillCommand(
            ExpenseListId: expenseList.Id,
            SupplierId: 5,
            ItemId: 99,
            Currency: "USD",
            CreatePayment: true,
            PaymentAccountId: 10,
            PaymentDateUtc: null,
            DateUtc: "2024-01-05T10:00:00Z"
        );

        // Mock CreateInvoiceCommand response
        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateInvoiceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDummyInvoiceResult(2002));

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(m => m.Send(It.IsAny<CreatePaymentCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
