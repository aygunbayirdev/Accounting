using Accounting.Application.Common.JsonConverters;
using Accounting.Application.Invoices.Queries.Dto;
using Accounting.Domain.Enums;
using MediatR;
using System.Text.Json.Serialization;

public sealed record UpdateInvoiceCommand(
    int Id,
    DateTime DateUtc,
    string Currency,
    int ContactId,
    InvoiceType Type,
    DocumentType? DocumentType,
    string? WaybillNumber,
    DateTime? WaybillDateUtc,
    DateTime? PaymentDueDateUtc,
    IReadOnlyList<UpdateInvoiceLineDto> Lines,
    string RowVersionBase64
) : IRequest<InvoiceDetailDto>;

public sealed record UpdateInvoiceLineDto(
    int Id,
    int? ItemId,

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal Qty,

    [property: JsonConverter(typeof(UnitPriceJsonConverter))]
    decimal UnitPrice,

    int VatRate,

    [property: JsonConverter(typeof(PercentJsonConverter))]
    decimal? DiscountRate,

    int? WithholdingRate
);
