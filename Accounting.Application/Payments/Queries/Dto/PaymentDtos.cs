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
    string Amount,
    string Currency,
    string? Description,
    DateTime CreatedAtUtc
);

public record PaymentDetailDto(
    int Id,
    int AccountId,
    int? ContactId,
    int? LinkedInvoiceId,
    DateTime DateUtc,
    string Direction,
    string Amount,
    string Currency,
    string? Description,
    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
