namespace Accounting.Application.CashBankAccounts.Queries.Dto;

public record CashBankAccountListItemDto(
    int Id,
    int BranchId,
    string Code,
    string Type,        // "Cash" | "Bank"
    string Name,
    string? Iban,
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
    string RowVersion,      // base64
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
