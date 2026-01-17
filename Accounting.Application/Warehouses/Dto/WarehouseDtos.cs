namespace Accounting.Application.Warehouses.Dto;

public record WarehouseDetailDto(
    int Id,
    int BranchId,
    string Code,
    string Name,
    bool IsDefault,
    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record WarehouseListItemDto(
    int Id,
    int BranchId,
    string Code,
    string Name,
    bool IsDefault,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
