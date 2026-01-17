using Accounting.Application.Common.Constants;
using Accounting.Application.Common.Models;
using Accounting.Application.Orders.Dto;
using Accounting.Domain.Enums;
using MediatR;

namespace Accounting.Application.Orders.Queries.List;

public record ListOrdersQuery(
    int? BranchId = null,
    int? ContactId = null,
    OrderStatus? Status = null,
    int Page = PaginationConstants.DefaultPage,
    int PageSize = PaginationConstants.DefaultPageSize
) : IRequest<PagedResult<OrderListItemDto>>;
