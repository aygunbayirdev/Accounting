namespace Accounting.Application.Users.Queries.Dto;

public record UserListItemDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    int? BranchId,
    string? BranchName,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record UserDetailDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    int? BranchId,
    string? BranchName,
    bool IsActive,
    List<string> Roles,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
