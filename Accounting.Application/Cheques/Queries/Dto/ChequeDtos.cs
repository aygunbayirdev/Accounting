using Accounting.Application.Common.JsonConverters;
using System.Text.Json.Serialization;

namespace Accounting.Application.Cheques.Queries.Dto;

public record ChequeDetailDto(
    int Id,
    int BranchId,
    string ChequeNumber,
    string Type,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Amount,

    DateTime DueDateUtc,
    string? DrawerName,
    string? BankName,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record ChequeListItemDto(
    int Id,
    int BranchId,
    string ChequeNumber,
    string Type,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Amount,

    DateTime DueDateUtc,
    string? DrawerName,
    string? BankName,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
