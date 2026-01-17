using Accounting.Application.Common.JsonConverters;
using System.Text.Json.Serialization;

namespace Accounting.Application.Stocks.Queries.Dto;

public record StockListItemDto(
    int Id,
    int BranchId,
    int WarehouseId,
    string WarehouseCode,
    int ItemId,
    string ItemCode,
    string ItemName,
    string Unit,

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal Quantity,

    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record StockDetailDto(
    int Id,
    int BranchId,
    int WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    int ItemId,
    string ItemCode,
    string ItemName,
    string Unit,

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal Quantity,

    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

