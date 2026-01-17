using Accounting.Application.Common.JsonConverters;
using Accounting.Application.Orders.Dto;
using MediatR;
using System.Text.Json.Serialization;

namespace Accounting.Application.Orders.Commands.Update;

public record UpdateOrderCommand(
    int Id,
    int ContactId,
    DateTime DateUtc,
    string? Description,
    List<UpdateOrderLineDto> Lines,
    string RowVersion
) : IRequest<OrderDetailDto>;

public record UpdateOrderLineDto(
    int? Id, // Null = New Line
    int? ItemId,
    string Description,

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal Quantity,

    [property: JsonConverter(typeof(UnitPriceJsonConverter))]
    decimal UnitPrice,

    int VatRate
);