using Accounting.Application.Warehouses.Dto;
using MediatR;

namespace Accounting.Application.Warehouses.Commands.Update;

public record UpdateWarehouseCommand(
    int Id,
    int BranchId,
    string Code,
    string Name,
    bool IsDefault,
    string RowVersion
) : IRequest<WarehouseDetailDto>;
