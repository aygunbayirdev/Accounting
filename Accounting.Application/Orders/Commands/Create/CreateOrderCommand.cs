using Accounting.Application.Common.JsonConverters;
using Accounting.Application.Orders.Dto;
using Accounting.Domain.Enums;
using MediatR;
using System.Text.Json.Serialization;

namespace Accounting.Application.Orders.Commands.Create;

public record CreateOrderCommand(
int ContactId,
DateTime DateUtc,
InvoiceType Type,
string Currency,
string? Description,
List<CreateOrderLineDto> Lines
) : IRequest<OrderDto>;

public record CreateOrderLineDto(
    int? ItemId,
    string Description,

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal Quantity,

    [property: JsonConverter(typeof(UnitPriceJsonConverter))]
    decimal UnitPrice,

    int VatRate
);
