namespace Accounting.Application.Categories.Queries;

public record CategoryDetailDto(
    int Id,
    string Name,
    string? Description,
    string? Color,
    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record CategoryListItemDto(
    int Id,
    string Name,
    string? Description,
    string? Color,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
