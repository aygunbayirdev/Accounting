namespace Accounting.Application.ExpenseDefinitions.Queries.Dto;

public record ExpenseDefinitionListItemDto(
    int Id,
    string Code,
    string Name,
    int DefaultVatRate,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record ExpenseDefinitionDetailDto(
    int Id,
    string Code,
    string Name,
    int DefaultVatRate,
    bool IsActive,
    string RowVersion, // base64
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);