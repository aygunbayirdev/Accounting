using Accounting.Application.Common.JsonConverters;
using System.Text.Json.Serialization;

namespace Accounting.Application.Items.Queries.Dto;

public record ItemListItemDto(
    int Id,
    int? CategoryId,
    string? CategoryName,
    string Code,
    string Name,
    int Type,                       // ItemType enum (1=Inventory, 2=Service)
    string Unit,
    int VatRate,

    int DefaultWithholdingRate,     // Varsayılan tevkifat oranı

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal? PurchasePrice,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal? SalesPrice,

    DateTime CreatedAtUtc
);

public record ItemDetailDto(
    int Id,
    int? CategoryId,
    string? CategoryName,
    string Code,
    string Name,
    int Type,
    string Unit,
    int VatRate,

    int DefaultWithholdingRate,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal? PurchasePrice,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal? SalesPrice,

    string RowVersion, // base64
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
