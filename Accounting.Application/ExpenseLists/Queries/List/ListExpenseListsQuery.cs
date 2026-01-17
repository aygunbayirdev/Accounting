using Accounting.Application.Common.Models;
using Accounting.Application.Common.Constants;
using Accounting.Application.ExpenseLists.Dto;
using MediatR;

namespace Accounting.Application.ExpenseLists.Queries.List;

public record ListExpenseListsQuery(
    int? BranchId = null,
    int PageNumber = 1,
    int PageSize = PaginationConstants.DefaultPageSize,
    string? Status = null,  // "Draft", "Reviewed", "Posted"
    string? Sort = "createdAtUtc:desc"
) : IRequest<PagedResult<ExpenseListListItemDto>>;