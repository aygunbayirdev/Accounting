using Accounting.Application.Branches.Queries.Dto;
using Accounting.Application.Common.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Branches.Queries.List;

public sealed class ListBranchesHandler
    : IRequestHandler<ListBranchesQuery, IReadOnlyList<BranchListItemDto>>
{
    private readonly IAppDbContext _ctx;

    public ListBranchesHandler(IAppDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<IReadOnlyList<BranchListItemDto>> Handle(
        ListBranchesQuery request,
        CancellationToken ct)
    {
        // Varsayım: global query filter ile IsDeleted=false zaten uygulanıyor.
        // Yine de açıkça eklemek istersen:
        // .Where(b => !b.IsDeleted)

        var branches = await _ctx.Branches
            .AsNoTracking()
            .OrderBy(b => b.Code)
            .Select(x => new BranchListItemDto(
                x.Id,
                x.Code,
                x.Name,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            ))
            .ToListAsync(ct);

        return branches;
    }
}
