using Accounting.Application.Common.JsonConverters;
using System.Text.Json.Serialization;

namespace Accounting.Application.CashBankAccounts.Queries.Dto;

public record CashBankAccountListItemDto(
    int Id,
    int BranchId,
    string Code,
    string Type,        // "Cash" | "Bank"
    string Name,
    string? Iban,
    string Currency,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Balance,

    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record CashBankAccountDetailDto(
    int Id,
    int BranchId,
    string Code,
    string Type,
    string Name,
    string? Iban,
    string Currency,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Balance,

    string RowVersion,      // base64
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
