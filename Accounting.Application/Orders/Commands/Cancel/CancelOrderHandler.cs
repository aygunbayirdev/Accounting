using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions; // ApplyBranchFilter
using Accounting.Application.Common.Interfaces; // ICurrentUserService
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Orders.Commands.Cancel;

public class CancelOrderHandler : IRequestHandler<CancelOrderCommand, bool>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public CancelOrderHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(CancelOrderCommand r, CancellationToken ct)
    {
        var order = await _db.Orders
            .ApplyBranchFilter(_currentUserService)
            .FirstOrDefaultAsync(o => o.Id == r.Id && !o.IsDeleted, ct);
        if (order is null) throw new NotFoundException("Order", r.Id);

        if (order.Status == OrderStatus.Invoiced)
        {
            throw new BusinessRuleException("Faturalandırılmış siparişler iptal edilemez.");
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            throw new BusinessRuleException("Sipariş zaten iptal edilmiş.");
        }

        _db.Entry(order).Property(nameof(order.RowVersion)).OriginalValue = Convert.FromBase64String(r.RowVersion);

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAtUtc = DateTime.UtcNow;

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
