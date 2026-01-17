using Accounting.Application.Common.Models;
using Accounting.Application.Common.Constants;
using Accounting.Application.Warehouses.Dto;
using MediatR;

namespace Accounting.Application.Warehouses.Queries.List;

public record ListWarehousesQuery(
    int BranchId,
    string? Search = null,           // code/name
    int PageNumber = 1,
    int PageSize = PaginationConstants.DefaultPageSize,
    string? Sort = "name:asc"        // code/name/isDefault
) : IRequest<PagedResult<WarehouseListItemDto>>;
