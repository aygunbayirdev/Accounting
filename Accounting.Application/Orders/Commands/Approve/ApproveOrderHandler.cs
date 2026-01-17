using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions; // ApplyBranchFilter
using Accounting.Application.Common.Interfaces; // ICurrentUserService
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Orders.Commands.Approve;

public class ApproveOrderHandler : IRequestHandler<ApproveOrderCommand, bool>
{
    private readonly IAppDbContext _db;
    private readonly IStockService _stockService;
    private readonly ICurrentUserService _currentUserService;

    public ApproveOrderHandler(IAppDbContext db, IStockService stockService, ICurrentUserService currentUserService)
    {
        _db = db;
        _stockService = stockService;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(ApproveOrderCommand request, CancellationToken ct)
    {
        var order = await _db.Orders
            .ApplyBranchFilter(_currentUserService)
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted, ct);

        if (order == null)
            throw new NotFoundException("Order", request.Id);

        // Optimistic Concurrency Check
        _db.Entry(order).Property("RowVersion").OriginalValue = request.RowVersion;

        if (order.Status != OrderStatus.Draft)
            throw new BusinessRuleException("Sadece 'Taslak' durumundaki siparişler onaylanabilir.");

        // Validate Stock for Sales Orders
        if (order.Type == InvoiceType.Sales)
        {
            foreach (var line in order.Lines)
            {
                if (line.ItemId.HasValue)
                {
                    await _stockService.ValidateStockAvailabilityAsync(line.ItemId.Value, line.Quantity, ct);
                }
            }
        }

        order.Status = OrderStatus.Approved;

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
