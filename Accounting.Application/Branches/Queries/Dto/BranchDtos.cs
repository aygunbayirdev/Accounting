namespace Accounting.Application.Branches.Queries.Dto;

public record BranchDetailDto(
    int Id, 
    string Code, 
    string Name, 
    string RowVersion,
    DateTime CreatedAtUtc, 
    DateTime? UpdatedAtUtc
);

public record BranchListItemDto(
    int Id, 
    string Code, 
    string Name, 
    DateTime CreatedAtUtc, 
    DateTime? UpdatedAtUtc
);
