using Accounting.Application.Common.JsonConverters;
using System.Text.Json.Serialization;

namespace Accounting.Application.Invoices.Queries.Dto;

public record InvoiceLineDto(
    int Id,
    int? ItemId,
    int? ExpenseDefinitionId,
    string ItemCode,
    string ItemName,
    string Unit,

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal Qty,

    [property: JsonConverter(typeof(UnitPriceJsonConverter))]
    decimal UnitPrice,

    int VatRate,

    [property: JsonConverter(typeof(PercentJsonConverter))]
    decimal DiscountRate,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal DiscountAmount,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Net,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Vat,

    int WithholdingRate,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal WithholdingAmount,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Gross,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal GrandTotal
);

public record InvoiceDto(
    int Id,
    int ContactId,
    string ContactCode,
    string ContactName,
    DateTime DateUtc,        // Belge tarihi (iş mantığı)
    string InvoiceNumber,
    string Currency,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalLineGross,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalDiscount,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalNet,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalVat,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalWithholding,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalGross,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Balance,


    IReadOnlyList<InvoiceLineDto> Lines,
    string RowVersion,       // base64
    DateTime CreatedAtUtc,   // Audit
    DateTime? UpdatedAtUtc,   // Audit
    int Type,
    int BranchId,
    string BranchCode,
    string BranchName,
    string? WaybillNumber,
    DateTime? WaybillDateUtc,
    DateTime? PaymentDueDateUtc
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

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalNet,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalVat,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalGross,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Balance,

    DateTime CreatedAtUtc,
    int BranchId,
    string BranchCode,
    string BranchName
);
