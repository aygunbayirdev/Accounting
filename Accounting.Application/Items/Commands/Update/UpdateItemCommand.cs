using Accounting.Application.Common.JsonConverters;
using Accounting.Application.Items.Queries.Dto;
using MediatR;
using System.Data;
using System.Text.Json.Serialization;

namespace Accounting.Application.Items.Commands.Update;

public record UpdateItemCommand(
    int Id,
    int? CategoryId,
    string Code,
    string Name,
    int Type, // ItemType: 1=Inventory, 2=Service
    string Unit,
    int VatRate,

    [property: JsonConverter(typeof(PercentJsonConverter))]
    int? DefaultWithholdingRate, // Varsayılan tevkifat oranı (%)

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal? PurchasePrice,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal? SalesPrice,

    string RowVersion // base64
) : IRequest<ItemDetailDto>;
