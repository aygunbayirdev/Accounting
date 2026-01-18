using Accounting.Application.Common.JsonConverters;
using Accounting.Application.Items.Queries.Dto;
using MediatR;
using System.Text.Json.Serialization;

namespace Accounting.Application.Items.Commands.Create;

public record CreateItemCommand(
    int? CategoryId,
    string Code,
    string Name,
    int Type,
    string Unit,
    int VatRate,
    int? DefaultWithholdingRate, // Varsayılan tevkifat oranı (%)

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal? PurchasePrice,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal? SalesPrice,

    string? PurchaseAccountCode,
    string? SalesAccountCode,
    int? UsefulLifeYears
) : IRequest<ItemDetailDto>;
