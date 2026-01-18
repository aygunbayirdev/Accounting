using Accounting.Application.Common.Abstractions;
using Accounting.Application.Reports.Queries.Dtos;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Reports.Queries.GetProfitLoss;

public class GetProfitLossHandler(IAppDbContext db) : IRequestHandler<GetProfitLossQuery, ProfitLossDto>
{
    public async Task<ProfitLossDto> Handle(GetProfitLossQuery request, CancellationToken ct)
    {
        // Date Filtering
        var dateFrom = request.DateFrom ?? DateTime.MinValue;
        var dateTo = request.DateTo ?? DateTime.MaxValue;

        // 1. Invoices (Income & COGS)
        var invoicesQuery = db.Invoices
            .AsNoTracking()
            .Where(i => i.DateUtc >= dateFrom && i.DateUtc <= dateTo && !i.IsDeleted);

        // Branch filter
        if (request.BranchId.HasValue)
            invoicesQuery = invoicesQuery.Where(i => i.BranchId == request.BranchId.Value);

        var invoices = await invoicesQuery
            .Select(i => new { i.Type, i.TotalNet, i.TotalVat })
            .ToListAsync(ct);

        var income = invoices.Where(i => i.Type == InvoiceType.Sales).Sum(i => i.TotalNet);
        var cogs = invoices.Where(i => i.Type == InvoiceType.Purchase).Sum(i => i.TotalNet);
        var invoiceVat = invoices.Sum(i => i.Type == InvoiceType.Sales ? i.TotalVat : -i.TotalVat);

        // 2. Expenses (Purchase Invoice içindeki Expense tipindeki Item'lar)
        var expenseLinesQuery = db.InvoiceLines
            .AsNoTracking()
            .Include(l => l.Invoice)
            .Include(l => l.Item)
            .Where(l => !l.IsDeleted
                && l.Invoice.Type == InvoiceType.Purchase
                && l.Invoice.DateUtc >= dateFrom
                && l.Invoice.DateUtc <= dateTo
                && !l.Invoice.IsDeleted
                && l.Item != null
                && l.Item.Type == ItemType.Expense);  // Sadece Expense tipindeki Item'lar

        // Branch filter
        if (request.BranchId.HasValue)
            expenseLinesQuery = expenseLinesQuery.Where(l => l.Invoice.BranchId == request.BranchId.Value);

        var expenseLines = await expenseLinesQuery
            .Select(l => new { l.Net, l.Vat })
            .ToListAsync(ct);

        var totalExpenses = expenseLines.Sum(e => e.Net);
        var expenseVat = expenseLines.Sum(e => e.Vat);

        // 3. Totals
        var grossProfit = income - cogs;
        var netProfit = grossProfit - totalExpenses;
        var totalVat = invoiceVat - expenseVat;

        return new ProfitLossDto(
            income,
            cogs,
            totalExpenses,
            grossProfit,
            netProfit,
            totalVat
        );
    }
}
