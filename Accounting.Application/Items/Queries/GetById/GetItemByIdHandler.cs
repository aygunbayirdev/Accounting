using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Items.Queries.Dto;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Items.Queries.GetById;

public class GetItemByIdHandler : IRequestHandler<GetItemByIdQuery, ItemDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetItemByIdHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<ItemDetailDto> Handle(GetItemByIdQuery r, CancellationToken ct)
    {
        var item = await _db.Items
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService)
            .Include(x => x.Category)
            .Where(x => x.Id == r.Id && !x.IsDeleted)
            .Select(x => new ItemDetailDto(
                x.Id,
                x.CategoryId,
                x.Category == null ? null : x.Category.Name,
                x.Code,
                x.Name,
                (int)x.Type,
                x.Unit,
                x.VatRate,
                x.DefaultWithholdingRate ?? 0,
                x.PurchasePrice,
                x.SalesPrice,
                x.PurchaseAccountCode,
                x.SalesAccountCode,
                x.UsefulLifeYears,
                Convert.ToBase64String(x.RowVersion),
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .FirstOrDefaultAsync(ct);

        if (item == null)
            throw new NotFoundException("Item", r.Id);

        return item;
    }
}
