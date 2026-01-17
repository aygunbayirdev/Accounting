using Accounting.Application.Common.JsonConverters;
using System.Text.Json.Serialization;

namespace Accounting.Application.FixedAssets.Queries.Dto;

public sealed record FixedAssetListItemDto(
    int Id,
    string Code,
    string Name,
    DateTime PurchaseDateUtc,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal PurchasePrice,

    int UsefulLifeYears,

    [property: JsonConverter(typeof(PercentJsonConverter))]
    decimal DepreciationRatePercent,

    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public sealed record FixedAssetDetailDto(
    int Id,
    string Code,
    string Name,
    DateTime PurchaseDateUtc,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal PurchasePrice,

    int UsefulLifeYears,

    [property: JsonConverter(typeof(PercentJsonConverter))]
    decimal DepreciationRatePercent,

    bool IsDeleted,
    string RowVersionBase64,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
