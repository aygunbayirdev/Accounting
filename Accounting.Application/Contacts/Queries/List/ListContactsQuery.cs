using Accounting.Application.Common.Constants;
using Accounting.Application.Common.Models;
using Accounting.Application.Contacts.Queries.Dto;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;

namespace Accounting.Application.Contacts.Queries.List;

public record ListContactsQuery(
    int? BranchId,
    string? Search,
    string? Sort,
    // Filters
    bool? IsCustomer,
    bool? IsVendor,
    bool? IsEmployee,
    bool? IsRetail,
    // Paging
    int PageNumber = PaginationConstants.DefaultPage,
    int PageSize = PaginationConstants.DefaultPageSize
    ) : IRequest<PagedResult<ContactListItemDto>>;