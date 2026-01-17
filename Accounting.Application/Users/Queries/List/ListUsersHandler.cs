using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Constants;
using Accounting.Application.Common.Models;
using Accounting.Application.Users.Queries.Dto;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Users.Queries.List;

public class ListUsersHandler : IRequestHandler<ListUsersQuery, PagedResult<UserListItemDto>>
{
    private readonly IAppDbContext _db;

    public ListUsersHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<UserListItemDto>> Handle(ListUsersQuery request, CancellationToken ct)
    {
        var page = PaginationConstants.NormalizePage(request.Page);
        var pageSize = PaginationConstants.NormalizePageSize(request.PageSize);

        var query = _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted);

        // Filters
        if (request.BranchId.HasValue)
            query = query.Where(u => u.BranchId == request.BranchId.Value);

        if (request.RoleId.HasValue)
            query = query.Where(u => u.UserRoles.Any(ur => ur.RoleId == request.RoleId.Value));

        if (request.IsActive.HasValue)
            query = query.Where(u => u.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(u => 
                u.FirstName.ToLower().Contains(search) ||
                u.LastName.ToLower().Contains(search) ||
                u.Email.ToLower().Contains(search)
            );
        }

        var total = await query.CountAsync(ct);

        var users = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserListItemDto(
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.BranchId,
                u.Branch != null ? u.Branch.Name : null,
                u.IsActive,
                u.CreatedAtUtc,
                u.UpdatedAtUtc
            ))
            .ToListAsync(ct);

        return new PagedResult<UserListItemDto>(total, page, pageSize, users, null);
    }
}
