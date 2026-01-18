using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Invoices.Commands.Create;
using Accounting.Application.Invoices.Commands.Update;
using Accounting.Application.Invoices.Queries.GetById;
using Accounting.Application.Invoices.Queries.List;
using Accounting.Application.Invoices.Queries.Dto;
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

public class InvoicesTests
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IStockService> _stockServiceMock;
    private readonly Mock<IInvoiceNumberService> _invoiceNumberServiceMock;
    private readonly Mock<IInvoiceBalanceService> _balanceServiceMock;

    public InvoicesTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _mediatorMock = new Mock<IMediator>();
        _stockServiceMock = new Mock<IStockService>();
        _invoiceNumberServiceMock = new Mock<IInvoiceNumberService>();
        _balanceServiceMock = new Mock<IInvoiceBalanceService>();
    }

    [Fact]
    public async Task CreateInvoice_ShouldSucceed_WhenValidData()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        // Seed
        db.Branches.Add(new Branch { Id = 1, Name = "Main Branch", Code = "BR-01" });
        db.Contacts.Add(new Contact { Id = 1, BranchId = 1, Name = "Test Customer", Code = "C-01", IsCustomer = true });
        db.Items.Add(new Item { Id = 10, BranchId = 1, Name = "Item A", Code = "I-01", Unit = "adet", VatRate = 20, SalesPrice = 100m });
        db.Warehouses.Add(new Warehouse { Id = 1, BranchId = 1, Name = "Main Warehouse", Code = "WH-01", IsDefault = true, RowVersion = Array.Empty<byte>() });
        await db.SaveChangesAsync();

        // Mocks
        _invoiceNumberServiceMock
            .Setup(x => x.GenerateNextAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("INV-2023-001");

        _stockServiceMock
            .Setup(x => x.ValidateBatchStockAvailabilityAsync(It.IsAny<Dictionary<int, decimal>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreateInvoiceHandler(db, _mediatorMock.Object, _stockServiceMock.Object, userService, _invoiceNumberServiceMock.Object);

        var command = new CreateInvoiceCommand(
            ContactId: 1,
            DateUtc: DateTime.UtcNow,
            Currency: "TRY",
            Type: InvoiceType.Sales,
            DocumentType: DocumentType.Invoice,
            WaybillNumber: null,
            WaybillDateUtc: null,
            PaymentDueDateUtc: DateTime.UtcNow.AddDays(7),
            Lines: new List<CreateInvoiceLineDto>
            {
                new CreateInvoiceLineDto(10, 1.00m, 100.00m, 20, 0.00m, 0)
            }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(0, result.Id);
        var invoice = await db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == result.Id);
        Assert.NotNull(invoice);
        Assert.Equal("INV-2023-001", invoice.InvoiceNumber);
        Assert.Single(invoice.Lines);
        Assert.Equal(100m, invoice.TotalLineGross);
        Assert.Equal(20m, invoice.TotalVat);
        Assert.Equal(120m, invoice.TotalGross);
    }

    [Fact]
    public async Task UpdateInvoice_ShouldSucceed()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        // Seed
        db.Branches.Add(new Branch { Id = 1, Name = "Main Branch", Code = "BR-01" });
        db.Contacts.Add(new Contact { Id = 1, BranchId = 1, Name = "Test Customer", Code = "C-01", IsCustomer = true });
        db.Items.Add(new Item { Id = 10, BranchId = 1, Name = "Item A", Code = "I-01", Unit = "adet", VatRate = 20, SalesPrice = 100m });
        db.Warehouses.Add(new Warehouse { Id = 1, BranchId = 1, Name = "Main Warehouse", Code = "WH-01", IsDefault = true, RowVersion = Array.Empty<byte>() });
        
        var invoice = new Invoice
        {
            BranchId = 1,
            ContactId = 1,
            InvoiceNumber = "INV-001",
            DateUtc = DateTime.UtcNow,
            Type = InvoiceType.Sales,
            RowVersion = Array.Empty<byte>()
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var handler = new UpdateInvoiceHandler(db, _balanceServiceMock.Object, _mediatorMock.Object, userService);

        var command = new UpdateInvoiceCommand(
            Id: invoice.Id,
            RowVersionBase64: Convert.ToBase64String(invoice.RowVersion),
            DateUtc: DateTime.UtcNow,
            Currency: "TRY",
            ContactId: 1,
            Type: InvoiceType.Sales,
            DocumentType: DocumentType.Invoice,
            WaybillNumber: null,
            WaybillDateUtc: null,
            PaymentDueDateUtc: null,
            Lines: new List<UpdateInvoiceLineDto>
            {
                // New Line
                new UpdateInvoiceLineDto(0, 10, 2.00m, 100.00m, 20, 0.00m, 0) 
            }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, result.Lines.Count); // use Lines instead of Items
        Assert.Equal(200.00m, result.TotalLineGross); // 2 * 100
    }

    [Fact]
    public async Task GetInvoiceById_ShouldReturnInvoice()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Main Branch", Code = "BR-01" });
        db.Contacts.Add(new Contact { Id = 1, BranchId = 1, Name = "Customer", Code = "C-01" });
        
        var invoice = new Invoice
        {
            BranchId = 1,
            ContactId = 1,
            InvoiceNumber = "INV-GET-01",
            RowVersion = Array.Empty<byte>()
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var handler = new GetInvoiceByIdHandler(db, userService);
        var result = await handler.Handle(new GetInvoiceByIdQuery(invoice.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("INV-GET-01", result.InvoiceNumber);
    }

    [Fact]
    public async Task ListInvoices_ShouldReturnPagedResults()
    {
        var userService = new FakeCurrentUserService(branchId: 1);
        var audit = new AuditSaveChangesInterceptor(userService);
        using var db = new AppDbContext(_options, audit, userService);

        db.Branches.Add(new Branch { Id = 1, Name = "Main Branch", Code = "BR-01" });
        db.Contacts.Add(new Contact { Id = 1, BranchId = 1, Name = "Customer", Code = "C-01" });

        for (int i = 0; i < 3; i++)
        {
            db.Invoices.Add(new Invoice
            {
                BranchId = 1,
                ContactId = 1,
                InvoiceNumber = $"INV-{i}",
                DateUtc = DateTime.UtcNow,
                RowVersion = Array.Empty<byte>()
            });
        }
        await db.SaveChangesAsync();

        var handler = new ListInvoicesHandler(db, userService);
        var result = await handler.Handle(new ListInvoicesQuery(PageSize: 10), CancellationToken.None);

        Assert.Equal(3, result.Total);
    }
}
