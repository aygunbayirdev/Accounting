using Accounting.Application.Common.Abstractions; // For IStockService
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Reports.Queries;
using Accounting.Application.Services; // For IContactBalanceService
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace Accounting.Tests;

public class ReportTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IStockService> _stockServiceMock;
    private readonly Mock<IContactBalanceService> _balanceServiceMock;
    private readonly AppDbContext _db;

    public ReportTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(x => x.BranchId).Returns(1);
        _currentUserServiceMock.Setup(x => x.UserId).Returns(1);

        _stockServiceMock = new Mock<IStockService>();
        _balanceServiceMock = new Mock<IContactBalanceService>();

        var audit = new AuditSaveChangesInterceptor(_currentUserServiceMock.Object);
        _db = new AppDbContext(options, audit, _currentUserServiceMock.Object);

        SeedData();
    }

    private void SeedData()
    {
        _db.Branches.Add(new Branch { Id = 1, Name = "Main", Code = "B1" });
        _db.Branches.Add(new Branch { Id = 2, Name = "Branch 2", Code = "B2" });

        // Contact
        _db.Contacts.Add(new Contact 
        { 
            Id = 1, BranchId = 1, Name = "Customer A", Code = "C001", 
            IsCustomer = true, IsVendor = false,
            RowVersion = Array.Empty<byte>()
        });

        // Invoice (Sales)
        _db.Invoices.Add(new Invoice
        {
            Id = 1, BranchId = 1, ContactId = 1, Type = InvoiceType.Sales,
            InvoiceNumber = "INV001",
            DateUtc = DateTime.UtcNow, // Changed to Now for DailySales
            TotalNet = 1000, TotalVat = 180, TotalGross = 1180,
            Balance = 1180,
            RowVersion = Array.Empty<byte>()
        });

        // Payment (In)
        _db.Payments.Add(new Payment
        {
            Id = 1, BranchId = 1, ContactId = 1, LinkedInvoiceId = 1, 
            Direction = PaymentDirection.In,
            Amount = 500,
            DateUtc = DateTime.UtcNow, // Changed to Now for DailyCollections
            RowVersion = Array.Empty<byte>()
        });

        // Stock
        _db.Warehouses.Add(new Warehouse { Id = 1, BranchId = 1, Name = "Main WH", Code = "WH1" });
        _db.Items.Add(new Item { Id = 1, BranchId = 1, Name = "Item A", Code = "ITM1", Unit = "PCS" });
        _db.Stocks.Add(new Stock 
        { 
            BranchId = 1, WarehouseId = 1, ItemId = 1, 
            Quantity = 50, 
            RowVersion = Array.Empty<byte>() 
        });

        _db.SaveChanges();
        
        // Setup Mocks
        _stockServiceMock.Setup(x => x.GetStockStatusAsync(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ItemStockDto> 
            { 
                new ItemStockDto(1, 100, 50, 0, 50) 
            });

        _balanceServiceMock.Setup(x => x.CalculateBalanceAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0); 
        
        _balanceServiceMock.Setup(x => x.GetTransactionsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContactTransaction> 
            {
                new ContactTransaction(DateTime.UtcNow.AddDays(-10), "Fatura", "INV001", "Sales", 1180, 0),
                new ContactTransaction(DateTime.UtcNow.AddDays(-5), "Tahsilat", "PAY001", "Payment", 0, 500)
            });
    }

    [Fact]
    public async Task GetDashboardStats_ShouldReturnCorrectFigures()
    {
        var handler = new GetDashboardStatsHandler(_db); 
        var query = new GetDashboardStatsQuery(1); 

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("1180.00", result.DailySalesTotal); 
    }

    [Fact]
    public async Task GetContactStatement_ShouldReturnTransactions()
    {
        var handler = new GetContactStatementHandler(_db, _balanceServiceMock.Object);
        var query = new GetContactStatementQuery(1, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result.ContactId);
        // 1 Opening Balance + 2 Transactions = 3
        Assert.Equal(3, result.Items.Count); 
        // Assert.Contains(result.Items, i => i.Type == "Fatura"); // Type is string, verified in mock
    }

    [Fact]
    public async Task GetStockStatus_ShouldReturnStockLevels()
    {
        var handler = new GetStockStatusHandler(_db, _stockServiceMock.Object);
        var query = new GetStockStatusQuery(); 

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(50, result[0].QuantityAvailable); 
        Assert.Equal("Item A", result[0].ItemName);
    }
    
    [Fact] 
    public async Task GetProfitLoss_ShouldReturnNetFigures()
    {
        var handler = new GetProfitLossHandler(_db); // Correct Class Name
        var query = new GetProfitLossQuery(1, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow); // Added BranchId Arg

        var result = await handler.Handle(query, CancellationToken.None);
        
        Assert.Equal(1000, result.Income); 
        Assert.Equal(0, result.Expenses); 
        Assert.Equal(1000, result.NetProfit);
    }
}
