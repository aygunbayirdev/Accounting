using Accounting.Application.Common.JsonConverters;
using Accounting.Application.Invoices.Queries.Dto;
using Accounting.Domain.Enums;
using MediatR;
using System.Text.Json.Serialization;

public sealed record UpdateInvoiceCommand(
    int Id,
    string RowVersionBase64,
    DateTime DateUtc,
    string Currency,
    int ContactId,
    InvoiceType Type,
    string? WaybillNumber,
    DateTime? WaybillDateUtc,
    DateTime? PaymentDueDateUtc,
    IReadOnlyList<UpdateInvoiceLineDto> Lines
) : IRequest<InvoiceDto>;

public sealed record UpdateInvoiceLineDto(
    int Id,
    int? ItemId,
    int? ExpenseDefinitionId,

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal Qty,

    [property: JsonConverter(typeof(UnitPriceJsonConverter))]
    decimal UnitPrice,

    int VatRate,

    [property: JsonConverter(typeof(PercentJsonConverter))]
    decimal? DiscountRate,

    [property: JsonConverter(typeof(PercentJsonConverter))]
    int? WithholdingRate
);
