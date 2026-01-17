using Accounting.Application.Common.JsonConverters;
using Accounting.Domain.Enums;
using System.Text.Json.Serialization;

namespace Accounting.Application.Orders.Dto;

public record OrderDetailDto(
    int Id,
    int BranchId,
    string OrderNumber,
    int ContactId,
    string ContactName,
    DateTime DateUtc,
    OrderStatus Status,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalNet,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalVat,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalGross,

    string Currency,
    string? Description,
    List<OrderLineDto> Lines,
    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record OrderListItemDto(
    int Id,
    int BranchId,
    string OrderNumber,
    int ContactId,
    string ContactName,
    DateTime DateUtc,
    OrderStatus Status,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalNet,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalVat,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalGross,

    string Currency,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record OrderLineDto(
    int Id,
    int? ItemId,
    string? ItemName,
    string Description,

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal Quantity,

    [property: JsonConverter(typeof(UnitPriceJsonConverter))]
    decimal UnitPrice,

    int VatRate,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Total
);
