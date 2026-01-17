using Accounting.Application.Common.JsonConverters;
using System.Text.Json.Serialization;

namespace Accounting.Application.Reports.Queries;

public record ContactStatementDto(
    int ContactId,
    string ContactName,
    List<StatementItemDto> Items
);

public record StatementItemDto(
    DateTime DateUtc,
    string Type,        // "Fatura", "Tahsilat", "Ödeme"
    string DocumentNo,
    string Description,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Debt,       // Borç (Müşteri Borçlandı / Biz Mal Sattık)

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Credit,     // Alacak (Müşteri Ödedi / Biz Mal Aldık)

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Balance     // Bakiye (Borç - Alacak)
);
