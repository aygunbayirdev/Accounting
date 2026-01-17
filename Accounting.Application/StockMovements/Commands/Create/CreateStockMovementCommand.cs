using Accounting.Application.Common.JsonConverters;
using Accounting.Application.StockMovements.Queries.Dto;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using System.Text.Json.Serialization;

namespace Accounting.Application.StockMovements.Commands.Create;

public record CreateStockMovementCommand(
    int WarehouseId,
    int ItemId,
    StockMovementType Type,

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal Quantity,

    DateTime? TransactionDateUtc,
    string? Note,
    int? InvoiceId = null            // Fatura kaynaklı hareketler için
) : IRequest<StockMovementDto>;
