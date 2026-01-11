using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Utils;
using Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Services;

/// <summary>
/// Invoice balance hesaplama ve güncelleme servisi.
/// Balance = TotalGross - SUM(Payments linked to this invoice)
/// 
/// CONCURRENCY HANDLING: 
/// - Optimistic concurrency via RowVersion
/// - Retry pattern for concurrent updates
/// - Cross-platform (SQL Server, PostgreSQL, SQLite)
/// </summary>
public interface IInvoiceBalanceService
{
    /// <summary>
    /// Belirtilen invoice'un güncel balance'ını hesaplar ve günceller.
    /// Concurrent updates için retry pattern kullanır.
    /// </summary>
    Task<decimal> RecalculateBalanceAsync(int invoiceId, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen invoice'un güncel balance'ını hesaplar (DB'ye yazmaz).
    /// Read-only - raporlama amaçlı kullanılabilir.
    /// </summary>
    Task<decimal> CalculateBalanceAsync(int invoiceId, CancellationToken ct = default);
}

public class InvoiceBalanceService : IInvoiceBalanceService
{
    private readonly IAppDbContext _db;
    private const int MaxRetries = 3;

    public InvoiceBalanceService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> CalculateBalanceAsync(int invoiceId, CancellationToken ct = default)
    {
        // Read-only hesaplama (raporlar için)
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => new { i.TotalGross, i.TotalWithholding })
            .FirstOrDefaultAsync(ct);

        if (invoice == null)
            return 0m;

        var totalPayments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.LinkedInvoiceId == invoiceId)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var payable = invoice.TotalGross - invoice.TotalWithholding;
        var balance = Money.R2(payable - totalPayments);

        return balance;
    }

    public async Task<decimal> RecalculateBalanceAsync(int invoiceId, CancellationToken ct = default)
    {
        // Retry pattern for optimistic concurrency conflicts
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await DoRecalculateAsync(invoiceId, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxRetries)
            {
                // Concurrency conflict - retry with fresh data
                // Detach tracked entities to get fresh data on next attempt
                var dbContext = _db as DbContext;
                if (dbContext != null)
                {
                    var trackedInvoice = dbContext.ChangeTracker
                        .Entries<Invoice>()
                        .FirstOrDefault(e => e.Entity.Id == invoiceId);
                    
                    if (trackedInvoice != null)
                    {
                        trackedInvoice.State = EntityState.Detached;
                    }
                }
                
                // Small delay before retry (exponential backoff)
                await Task.Delay(attempt * 50, ct);
            }
        }

        // Should not reach here, but if all retries fail, throw
        throw new InvalidOperationException(
            $"Failed to recalculate balance for Invoice {invoiceId} after {MaxRetries} attempts due to concurrent updates.");
    }

    private async Task<decimal> DoRecalculateAsync(int invoiceId, CancellationToken ct)
    {
        // Fetch invoice with tracking (for update)
        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (invoice == null)
            return 0m;

        // Calculate total payments from committed data
        // NOT: Burada AsNoTracking kullanıyoruz çünkü Payment handler'lar
        // artık önce SaveChanges yapıyor, sonra RecalculateBalance çağırıyor
        var totalPayments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.LinkedInvoiceId == invoiceId)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var payable = invoice.TotalGross - invoice.TotalWithholding;
        var balance = Money.R2(payable - totalPayments);
        invoice.Balance = balance;

        // RowVersion otomatik olarak concurrency check yapacak
        // Eğer başka bir transaction invoice'u değiştirdiyse,
        // DbUpdateConcurrencyException fırlatılır ve retry yapılır

        return balance;
    }
}