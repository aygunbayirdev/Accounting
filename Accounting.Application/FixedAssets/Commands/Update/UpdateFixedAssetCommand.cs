using Accounting.Application.Common.JsonConverters;
using Accounting.Application.FixedAssets.Queries.Dto;
using MediatR;
using System.Text.Json.Serialization;

namespace Accounting.Application.FixedAssets.Commands.Update;

public sealed record UpdateFixedAssetCommand(
    int Id,
    string RowVersionBase64,
    string Code,
    string Name,
    DateTime PurchaseDateUtc,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal PurchasePrice,

    int UsefulLifeYears
) : IRequest<FixedAssetDetailDto>;
