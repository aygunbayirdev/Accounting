using Accounting.Application.Common.JsonConverters;
using System.Text.Json.Serialization;

namespace Accounting.Application.Reports.Queries.Dtos;

public record ProfitLossDto(
    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Income,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal CostOfGoods,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Expenses,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal GrossProfit,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal NetProfit,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalVat
);
