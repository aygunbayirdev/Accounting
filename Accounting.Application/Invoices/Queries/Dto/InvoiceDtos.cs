namespace Accounting.Application.Invoices.Queries.Dto;

public record InvoiceLineDto(
    int Id,
    int? ItemId,
    int? ExpenseDefinitionId,
    string ItemCode,
    string ItemName,
    string Unit,
    string Qty,        // F3
    string UnitPrice,  // F4
    int VatRate,
    string DiscountRate, // F2 (Added)
    string DiscountAmount, // F2 (Added)
    string Net,        // F2
    string Vat,        // F2
    int WithholdingRate, // (Added)
    string WithholdingAmount, // F2 (Added)
    string Gross,       // F2
    string GrandTotal   // F2 (Added)
);

public record InvoiceDto(
    int Id,
    int ContactId,
    string ContactCode,
    string ContactName,
    DateTime DateUtc,        // Belge tarihi (iş mantığı)
    string InvoiceNumber,    // Added
    string Currency,
    string TotalLineGross,   // F2 (Added)
    string TotalDiscount,    // F2 (Added)
    string TotalNet,         // F2
    string TotalVat,         // F2
    string TotalWithholding, // F2 (Added)
    string TotalGross,       // F2
    string Balance,
    IReadOnlyList<InvoiceLineDto> Lines,
    string RowVersion,       // base64
    DateTime CreatedAtUtc,   // Audit
    DateTime? UpdatedAtUtc,   // Audit
    int Type,
    int BranchId,
    string BranchCode,
    string BranchName,
    string? WaybillNumber,     // (Added)
    DateTime? WaybillDateUtc,  // (Added)
    DateTime? PaymentDueDateUtc // (Added)
);

public record InvoiceListItemDto(
    int Id,
    int ContactId,
    string ContactCode,
    string ContactName,
    string InvoiceNumber,    // Fatura numarası
    string Type,             // Sales / Purchase
    DateTime DateUtc,
    string Currency,
    string TotalNet,
    string TotalVat,
    string TotalGross,
    string Balance,
    DateTime CreatedAtUtc,
    int BranchId,
    string BranchCode,
    string BranchName
);
