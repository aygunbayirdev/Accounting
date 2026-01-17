using Accounting.Application.Common.JsonConverters;
using Accounting.Application.FixedAssets.Queries.Dto;
using MediatR;
using System.Text.Json.Serialization;

namespace Accounting.Application.FixedAssets.Commands.Create;

public sealed record CreateFixedAssetCommand(
    int BranchId,
    string Code,
    string Name,
    DateTime PurchaseDateUtc,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal PurchasePrice,

    int UsefulLifeYears
) : IRequest<FixedAssetDetailDto>;
