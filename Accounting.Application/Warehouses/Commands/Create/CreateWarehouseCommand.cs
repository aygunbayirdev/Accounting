using Accounting.Application.Warehouses.Dto;
using MediatR;

namespace Accounting.Application.Warehouses.Commands.Create;

public record CreateWarehouseCommand(
    string Code,
    string Name,
    bool IsDefault
) : IRequest<WarehouseDetailDto>;
