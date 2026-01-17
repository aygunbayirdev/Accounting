using Accounting.Application.Warehouses.Dto;
using MediatR;

namespace Accounting.Application.Warehouses.Queries.GetById;

public record GetWarehouseByIdQuery(int Id) : IRequest<WarehouseDetailDto>;
