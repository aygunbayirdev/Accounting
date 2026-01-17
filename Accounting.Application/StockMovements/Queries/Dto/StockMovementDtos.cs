using Accounting.Application.Common.JsonConverters;
using Accounting.Domain.Enums;
using System.Text.Json.Serialization;

namespace Accounting.Application.StockMovements.Queries.Dto;

public record StockMovementDto(
    int Id,
    int BranchId,
    int WarehouseId,
    string WarehouseCode,
    int ItemId,
    string ItemCode,
    string ItemName,
    string Unit,
    StockMovementType Type,

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal Quantity,

    DateTime TransactionDateUtc,
    string? Note,
    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
