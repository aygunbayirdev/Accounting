using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Models;
using Accounting.Application.Common.Utils;
using Accounting.Application.Payments.Queries.Dto;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Accounting.Application.Payments.Queries.List
{
    public class ListPaymentsHandler : IRequestHandler<ListPaymentsQuery, PagedResult<PaymentListItemDto>>
    {
        private readonly IAppDbContext _db;
        private readonly ICurrentUserService _currentUserService;

        public ListPaymentsHandler(IAppDbContext db, ICurrentUserService currentUserService)
        {
            _db = db;
            _currentUserService = currentUserService;
        }

        public async Task<PagedResult<PaymentListItemDto>> Handle(ListPaymentsQuery q, CancellationToken ct)
        {
            var query = _db.Payments
                .AsNoTracking()
                .ApplyBranchFilter(_currentUserService);

            // --- Filtreler ---
            if (q.AccountId is int accId) query = query.Where(p => p.AccountId == accId);
            if (q.ContactId is int conId) query = query.Where(p => p.ContactId == conId);
            if (q.Direction is not null) query = query.Where(p => p.Direction == q.Direction);

            if (TryParseUtc(q.DateFromUtc, out var fromUtc)) query = query.Where(p => p.DateUtc >= fromUtc);
            if (TryParseUtc(q.DateToUtc, out var toUtc)) query = query.Where(p => p.DateUtc <= toUtc);

            if (!string.IsNullOrWhiteSpace(q.Currency))
            {
                var curr = q.Currency.Trim().ToUpperInvariant();
                query = query.Where(p => p.Currency == curr);
            }

            // --- Sıralama ---
            var sort = (q.Sort ?? "dateUtc:desc").Split(':');
            var field = sort[0].ToLowerInvariant();
            var dir = sort.Length > 1 ? sort[1].ToLowerInvariant() : "desc";

            query = (field, dir) switch
            {
                ("amount", "asc") => query.OrderBy(p => p.Amount),
                ("amount", "desc") => query.OrderByDescending(p => p.Amount),
                ("dateutc", "asc") => query.OrderBy(p => p.DateUtc),
                _ => query.OrderByDescending(p => p.DateUtc),
            };

            // --- Toplam kayıt sayısı ---
            var total = await query.CountAsync(ct);

            // --- Filtered toplam (sayfa sınırı yok) ---
            // Not: EF decimal sum -> DB tarafında yapılır; ardından 2 haneye AwayFromZero ile string formatlarız
            var filteredSum = await query.Select(p => (decimal?)p.Amount).SumAsync(ct) ?? 0m;

            // --- Sayfa verisi ---
            var pageQuery = query
                .Skip((q.PageNumber - 1) * q.PageSize)
                .Take(q.PageSize)
                .Select(p => new {
                    p.Id,
                    p.AccountId,
                    AccountCode = p.Account.Code,
                    AccountName = p.Account.Name,
                    p.ContactId,
                    ContactCode = p.ContactId != null ? p.Contact!.Code : null,
                    ContactName = p.ContactId != null ? p.Contact!.Name : null,
                    p.LinkedInvoiceId,
                    p.DateUtc,
                    p.Direction,
                    p.Amount,
                    p.Currency,
                    p.Description,
                    p.CreatedAtUtc,
                });

            var pageData = await pageQuery.ToListAsync(ct);

            // --- Page toplamı (sayfadaki kalemlerin toplamı) ---
            var pageSum = pageData.Aggregate(0m, (acc, x) => acc + x.Amount);

            var inv = CultureInfo.InvariantCulture;

            var items = pageData.Select(p => new PaymentListItemDto(
                p.Id,
                p.AccountId,
                p.AccountCode,
                p.AccountName,
                p.ContactId,
                p.ContactCode,
                p.ContactName,
                p.LinkedInvoiceId,
                p.DateUtc,
                p.Direction.ToString(),
                Money.S2(p.Amount),
                p.Currency,
                p.Description,
                p.CreatedAtUtc
            )).ToList();

            var totals = new PagedTotals(
                PageTotalAmount: Money.S2(pageSum),
                FilteredTotalAmount: Money.S2(filteredSum)
            );

            return new PagedResult<PaymentListItemDto>(total, q.PageNumber, q.PageSize, items, totals);
        }

        private static bool TryParseUtc(string? s, out DateTime value)
            => DateTime.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal, out value);
    }
}