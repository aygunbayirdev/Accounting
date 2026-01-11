using Accounting.Application.Common.Abstractions;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Services;

/// <summary>
/// Fatura numara oluşturucu.
/// Pattern: {TypePrefix}-{Year}-{6-digit sequence}
/// Örnek: SAT-2026-000001, ALI-2026-000042
/// </summary>
public class InvoiceNumberService(IAppDbContext db) : IInvoiceNumberService
{
    private static readonly Dictionary<InvoiceType, string> TypePrefixes = new()
    {
        { InvoiceType.Sales, "SAT" },
        { InvoiceType.Purchase, "ALI" },
        { InvoiceType.SalesReturn, "SIA" },
        { InvoiceType.PurchaseReturn, "AIA" },
        { InvoiceType.Expense, "MAS" }
    };

    public async Task<string> GenerateNextAsync(int branchId, string invoiceTypePrefix, CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"{invoiceTypePrefix}-{year}-";

        // Son fatura numarasını bul
        var lastInvoice = await db.Invoices
            .AsNoTracking()
            .Where(i => i.BranchId == branchId && i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync(ct);

        int nextSequence = 1;

        if (lastInvoice != null)
        {
            // Parse sequence from "SAT-2026-000042" -> 42
            var parts = lastInvoice.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var seq))
            {
                nextSequence = seq + 1;
            }
        }

        return $"{prefix}{nextSequence:D6}";
    }

    public static string GetPrefix(InvoiceType type) => 
        TypePrefixes.TryGetValue(type, out var p) ? p : "FAT";
}
