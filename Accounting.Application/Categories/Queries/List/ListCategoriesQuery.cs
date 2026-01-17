using Accounting.Application.Common.Models;
using MediatR;

namespace Accounting.Application.Categories.Queries.List;

public record ListCategoriesQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<PagedResult<CategoryListItemDto>>;
