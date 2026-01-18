using System.Globalization;
using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Constants;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Models;
using Accounting.Application.Common.Utils;
using Accounting.Application.Invoices.Queries.Dto;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Invoices.Queries.List;

public class ListInvoicesHandler : IRequestHandler<ListInvoicesQuery, PagedResult<InvoiceListItemDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public ListInvoicesHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<InvoiceListItemDto>> Handle(ListInvoicesQuery q, CancellationToken ct)
    {
        // Normalize pagination
        var pageNumber = PaginationConstants.NormalizePage(q.PageNumber);
        var pageSize = PaginationConstants.NormalizePageSize(q.PageSize);

        var query = _db.Invoices
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService); // ✅ DRY branch filtering

        // Filtreler
        if (q.BranchId is int branchId)
        {
            query = query.Where(i => i.BranchId == branchId);
        }

        if (q.ContactId is int cid) query = query.Where(i => i.ContactId == cid);

        // Type filtresi (varsa). Domain tarafında satış/alış ayırımını nasıl tuttuğuna göre uyarlayalım.
        // Örn: Invoice'ta bool IsPurchase gibi bir alan varsa:
        if (q.Type == InvoiceTypeFilter.Sales) query = query.Where(i => i.Type == InvoiceType.Sales);
        if (q.Type == InvoiceTypeFilter.Purchase) query = query.Where(i => i.Type == InvoiceType.Purchase);
        if (q.Type == InvoiceTypeFilter.PurchaseReturn) query = query.Where(i => i.Type == InvoiceType.PurchaseReturn);
        if (q.Type == InvoiceTypeFilter.SalesReturn) query = query.Where(i => i.Type == InvoiceType.SalesReturn);

        if (TryParseUtc(q.DateFromUtc, out var fromUtc)) query = query.Where(i => i.DateUtc >= fromUtc);
        if (TryParseUtc(q.DateToUtc, out var toUtc)) query = query.Where(i => i.DateUtc <= toUtc);

        // Sıralama
        var sort = (q.Sort ?? "dateUtc:desc").Split(':');
        var field = sort[0].ToLowerInvariant();
        var dir = sort.Length > 1 ? sort[1].ToLowerInvariant() : "desc";

        query = (field, dir) switch
        {
            ("totalgross", "asc") => query.OrderBy(i => i.TotalGross),
            ("totalgross", "desc") => query.OrderByDescending(i => i.TotalGross),
            ("dateutc", "asc") => query.OrderBy(i => i.DateUtc),
            _ => query.OrderByDescending(i => i.DateUtc),
        };

        // Toplam kayıt sayısı
        var total = await query.CountAsync(ct);

        // Filtered totals (DB tarafında SUM)
        var filteredNet = await query.Select(i => (decimal?)i.TotalNet).SumAsync(ct) ?? 0m;
        var filteredVat = await query.Select(i => (decimal?)i.TotalVat).SumAsync(ct) ?? 0m;
        var filteredGross = await query.Select(i => (decimal?)i.TotalGross).SumAsync(ct) ?? 0m;

        // Sayfa verisi
        var pageData = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new {
                i.Id,
                i.ContactId,
                i.InvoiceNumber,
                i.Type,
                i.DocumentType,
                i.DateUtc,
                i.Currency,
                i.TotalNet,
                i.TotalVat,
                i.TotalGross,
                i.Balance,
                i.CreatedAtUtc,
                i.UpdatedAtUtc,
                ContactCode = i.Contact.Code,
                ContactName = i.Contact.Name,
                i.BranchId,
                BranchCode = i.Branch.Code,
                BranchName = i.Branch.Name
            })
            .ToListAsync(ct);

        // Page totals (sayfadaki kalemlerin toplamı)
        var pageNet = pageData.Aggregate(0m, (acc, x) => acc + x.TotalNet);
        var pageVat = pageData.Aggregate(0m, (acc, x) => acc + x.TotalVat);
        var pageGross = pageData.Aggregate(0m, (acc, x) => acc + x.TotalGross);

        // Items
        var items = pageData.Select(i => new InvoiceListItemDto(
            i.Id,
            i.ContactId,
            i.ContactCode,
            i.ContactName,
            i.InvoiceNumber ?? string.Empty,
            (int)i.Type,
                (int)i.DocumentType,
            i.DateUtc,
            i.Currency,
            i.TotalNet,
            i.TotalVat,
            i.TotalGross,
            i.Balance,
            i.BranchId,
            i.BranchCode,
            i.BranchName,
            i.CreatedAtUtc,
            i.UpdatedAtUtc
        )).ToList();

        var totals = new InvoicePagedTotals(
            PageTotalNet: pageNet,
            PageTotalVat: pageVat,
            PageTotalGross: pageGross,
            FilteredTotalNet: filteredNet,
            FilteredTotalVat: filteredVat,
            FilteredTotalGross: filteredGross
        );

        return new PagedResult<InvoiceListItemDto>(total, pageNumber, pageSize, items, totals);
    }

    private static bool TryParseUtc(string? s, out DateTime value)
        => DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal, out value);
}
