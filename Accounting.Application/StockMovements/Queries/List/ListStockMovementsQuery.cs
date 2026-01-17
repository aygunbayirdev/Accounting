using Accounting.Application.Common.Models;
using Accounting.Application.Common.Constants;
using Accounting.Application.StockMovements.Queries.Dto;
using Accounting.Domain.Enums;
using MediatR;

namespace Accounting.Application.StockMovements.Queries.List;

public record ListStockMovementsQuery(
    int BranchId,
    int? WarehouseId = null,
    int? ItemId = null,
    StockMovementType? Type = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int PageNumber = 1,
    int PageSize = PaginationConstants.DefaultPageSize,
    string? Sort = "date:desc" // date/created/item
) : IRequest<PagedResult<StockMovementListItemDto>>;
