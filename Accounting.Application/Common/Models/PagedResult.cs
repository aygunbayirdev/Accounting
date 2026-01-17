namespace Accounting.Application.Common.Models;

public record PagedResult<T>(
    int Total,
    int PageNumber,
    int PageSize,
    IReadOnlyList<T> Items,
    object? Totals = null // Payments'da PagedTotals, Invoices'da InvoicePagedTotals geçeceğiz
    );

// For payment
public record PagedTotals(
    decimal? PageTotalAmount,
    decimal? FilteredTotalAmount
    );

public record InvoicePagedTotals(
    decimal PageTotalNet,
    decimal PageTotalVat,
    decimal PageTotalGross,
    decimal FilteredTotalNet,
    decimal FilteredTotalVat,
    decimal FilteredTotalGross
    );

