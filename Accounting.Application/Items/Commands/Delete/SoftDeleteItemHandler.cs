using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions; // ApplyBranchFilter
using Accounting.Application.Common.Interfaces; // ICurrentUserService
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Items.Commands.Delete;

public class SoftDeleteItemHandler : IRequestHandler<SoftDeleteItemCommand, bool>
{
    private readonly IAppDbContext _db;

    public SoftDeleteItemHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> Handle(SoftDeleteItemCommand r, CancellationToken ct)
    {
        var e = await _db.Items
            .FirstOrDefaultAsync(i => i.Id == r.Id && !i.IsDeleted, ct);
        if (e is null) throw new NotFoundException("Item", r.Id);

        _db.Entry(e).Property(nameof(Item.RowVersion)).OriginalValue = Convert.FromBase64String(r.RowVersion);

        e.IsDeleted = true;
        e.DeletedAtUtc = DateTime.UtcNow;
        e.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("Item silmede eşzamanlılık hatası.");
        }
    }
}
