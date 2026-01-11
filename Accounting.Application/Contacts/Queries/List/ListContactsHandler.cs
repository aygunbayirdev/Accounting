using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Constants;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Contacts.Queries.Dto;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Contacts.Queries.List;

public class ListContactsHandler : IRequestHandler<ListContactsQuery, ContactListResult>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    
    public ListContactsHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<ContactListResult> Handle(ListContactsQuery q, CancellationToken ct)
    {
        // Normalize pagination
        var page = PaginationConstants.NormalizePage(q.Page);
        var pageSize = PaginationConstants.NormalizePageSize(q.PageSize);

        var qry = _db.Contacts
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService);

        // BranchId filter
        if (q.BranchId.HasValue)
        {
            qry = qry.Where(x => x.BranchId == q.BranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            qry = qry.Where(x => x.Name.Contains(s) || x.Code.Contains(s) || (x.Email != null && x.Email.Contains(s)));
        }

        // Flag Filters
        if (q.IsCustomer.HasValue && q.IsCustomer.Value)
        {
            qry = qry.Where(x => x.IsCustomer);
        }
        if (q.IsVendor.HasValue && q.IsVendor.Value)
        {
            qry = qry.Where(x => x.IsVendor);
        }
        if (q.IsEmployee.HasValue && q.IsEmployee.Value)
        {
            qry = qry.Where(x => x.IsEmployee);
        }
        if (q.IsRetail.HasValue && q.IsRetail.Value)
        {
            qry = qry.Where(x => x.IsRetail);
        }

        var total = await qry.CountAsync(ct);

        var items = await qry
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ContactListItemDto(
                x.Id, 
                x.BranchId, 
                x.Code, 
                x.Name, 
                x.IsCustomer, 
                x.IsVendor, 
                x.IsEmployee, 
                x.IsRetail, 
                x.Email,
                x.CreatedAtUtc
            ))
            .ToListAsync(ct);

        return new ContactListResult(total, items);
    }
}
