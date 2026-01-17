using Accounting.Application.Common.JsonConverters;
using System.Text.Json.Serialization;

namespace Accounting.Application.Invoices.Commands.Create;

public record CreateInvoiceLineDto(
    int? ItemId,
    int? ExpenseDefinitionId,

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal Qty,

    [property: JsonConverter(typeof(UnitPriceJsonConverter))]
    decimal UnitPrice,

    int VatRate,

    [property: JsonConverter(typeof(PercentJsonConverter))]
    decimal? DiscountRate,
    int? WithholdingRate
);
