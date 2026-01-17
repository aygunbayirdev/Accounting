using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions; // ApplyBranchFilter
using Accounting.Application.Common.Interfaces; // ICurrentUserService
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Orders.Commands.Delete;

public class DeleteOrderHandler : IRequestHandler<DeleteOrderCommand, bool>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public DeleteOrderHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(DeleteOrderCommand r, CancellationToken ct)
    {
        var order = await _db.Orders
            .ApplyBranchFilter(_currentUserService)
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == r.Id && !o.IsDeleted, ct);

        if (order is null) throw new NotFoundException("Order", r.Id);

        if (order.Status != OrderStatus.Draft && order.Status != OrderStatus.Cancelled)
        {
            throw new BusinessRuleException("Sadece taslak veya iptal durumundaki siparişler silinebilir.");
        }

        _db.Entry(order).Property(nameof(order.RowVersion)).OriginalValue = Convert.FromBase64String(r.RowVersion);

        // Soft delete order
        order.IsDeleted = true;
        order.DeletedAtUtc = DateTime.UtcNow;

        // Soft delete all lines
        foreach (var line in order.Lines.Where(l => !l.IsDeleted))
        {
            line.IsDeleted = true;
            line.DeletedAtUtc = DateTime.UtcNow;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("Sipariş başka bir kullanıcı tarafından değiştirildi.");
        }

        return true;
    }
}
