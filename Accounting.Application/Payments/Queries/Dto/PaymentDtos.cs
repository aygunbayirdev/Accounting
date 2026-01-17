using Accounting.Application.Common.JsonConverters;
using System.Text.Json.Serialization;

namespace Accounting.Application.Payments.Queries.Dto;

public record PaymentListItemDto(
    int Id,
    int AccountId,
    string AccountCode,
    string AccountName,
    int? ContactId,
    string? ContactCode,
    string? ContactName,
    int? LinkedInvoiceId,
    DateTime DateUtc,
    string Direction,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Amount,

    string Currency,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record PaymentDetailDto(
    int Id,
    int AccountId,
    int? ContactId,
    int? LinkedInvoiceId,
    DateTime DateUtc,
    string Direction,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Amount,

    string Currency,
    string? Description,
    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
