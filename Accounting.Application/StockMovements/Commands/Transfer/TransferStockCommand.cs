using Accounting.Application.Common.JsonConverters;
using MediatR;
using System.Text.Json.Serialization;

namespace Accounting.Application.StockMovements.Commands.Transfer;

public record TransferStockCommand(
    int SourceWarehouseId,
    int TargetWarehouseId,
    int ItemId,

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal Quantity,

    DateTime TransactionDateUtc,
    string? Description
) : IRequest<StockTransferDetailDto>;
