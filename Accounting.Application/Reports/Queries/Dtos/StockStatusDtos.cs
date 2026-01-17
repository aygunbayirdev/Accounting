using Accounting.Application.Common.JsonConverters;
using System.Text.Json.Serialization;

namespace Accounting.Application.Reports.Queries.Dtos;

public record StockStatusDto(
    int ItemId,
    string ItemCode,
    string ItemName,
    string Unit,

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal QuantityIn,       // Giren

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal QuantityOut,      // Çıkan

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal QuantityReserved, // Rezerve

    [property: JsonConverter(typeof(QuantityJsonConverter))]
    decimal QuantityAvailable // Mevcut
);
