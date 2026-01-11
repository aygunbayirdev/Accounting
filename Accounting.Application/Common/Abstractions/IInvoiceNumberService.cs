namespace Accounting.Application.Common.Abstractions;

/// <summary>
/// Fatura numarası otomatik oluşturma servisi.
/// Format: {Prefix}-{Year}-{Sequence} (Örn: SAT-2026-000001)
/// </summary>
public interface IInvoiceNumberService
{
    /// <summary>
    /// Belirtilen şube ve fatura tipi için benzersiz fatura numarası oluşturur.
    /// </summary>
    Task<string> GenerateNextAsync(int branchId, string invoiceTypePrefix, CancellationToken ct = default);
}
