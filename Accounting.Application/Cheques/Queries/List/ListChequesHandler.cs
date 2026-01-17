using Accounting.Application.Cheques.Queries.Dto;
using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Constants;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Cheques.Queries.List;

public class ListChequesHandler : IRequestHandler<ListChequesQuery, PagedResult<ChequeDetailDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public ListChequesHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<ChequeDetailDto>> Handle(ListChequesQuery request, CancellationToken ct)
    {
        var page = PaginationConstants.NormalizePage(request.Page);
        var pageSize = PaginationConstants.NormalizePageSize(request.PageSize);

        var query = _db.Cheques
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService);

        // Filters
        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(c => c.Status.ToString() == request.Status);

        if (!string.IsNullOrWhiteSpace(request.Type))
            query = query.Where(c => c.Type.ToString() == request.Type);

        var total = await query.CountAsync(ct);

        var cheques = await query
            .OrderByDescending(c => c.DueDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ChequeDetailDto(
                c.Id,
                c.BranchId,
                c.ChequeNumber,
                c.Type.ToString(),
                c.Amount,
                c.DueDate,
                c.DrawerName,
                c.BankName,
                c.Status.ToString(),
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                Convert.ToBase64String(c.RowVersion)
            ))
            .ToListAsync(ct);

        return new PagedResult<ChequeDetailDto>(total, page, pageSize, cheques, null);
    }
}
