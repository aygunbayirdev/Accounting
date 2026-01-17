using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions; // ApplyBranchFilter
using Accounting.Application.Common.Interfaces; // ICurrentUserService
using Accounting.Application.Common.Utils;
using Accounting.Application.Orders.Dto;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Orders.Commands.Update;

public class UpdateOrderHandler : IRequestHandler<UpdateOrderCommand, OrderDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public UpdateOrderHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<OrderDetailDto> Handle(UpdateOrderCommand r, CancellationToken ct)
    {
        var order = await _db.Orders
            .ApplyBranchFilter(_currentUserService)
            .Include(o => o.Lines)
            .Include(o => o.Contact)
            .FirstOrDefaultAsync(o => o.Id == r.Id, ct);

        if (order is null) throw new NotFoundException("Order", r.Id);

        if (order.Status != OrderStatus.Draft)
        {
            throw new BusinessRuleException("Sadece taslak durumundaki siparişler güncellenebilir.");
        }

        _db.Entry(order).Property(nameof(order.RowVersion)).OriginalValue = Convert.FromBase64String(r.RowVersion);

        // Update Header
        order.ContactId = r.ContactId;
        order.DateUtc = r.DateUtc;
        order.Description = r.Description;
        order.UpdatedAtUtc = DateTime.UtcNow;

        // Update Lines
        // 1. Soft delete removed lines (hard delete yerine)
        var reqLineIds = r.Lines.Where(l => l.Id.HasValue).Select(l => l.Id!.Value).ToList();
        var toRemove = order.Lines.Where(l => !reqLineIds.Contains(l.Id)).ToList();
        foreach (var rm in toRemove)
        {
            rm.IsDeleted = true;
            rm.DeletedAtUtc = DateTime.UtcNow;
        }

        // 2. Add/Update lines
        decimal totalNet = 0;
        decimal totalVat = 0;

        foreach (var l in r.Lines)
        {
            var lineNet = DecimalExtensions.RoundAmount(l.Quantity * l.UnitPrice);
            var vatAmount = DecimalExtensions.RoundAmount(lineNet * l.VatRate / 100m);

            totalNet += lineNet;
            totalVat += vatAmount;

            if (l.Id.HasValue)
            {
                var existing = order.Lines.FirstOrDefault(x => x.Id == l.Id.Value);
                if (existing == null) continue; // Skip if not found or deleted

                existing.ItemId = l.ItemId;
                existing.Description = l.Description;
                existing.Quantity = l.Quantity;
                existing.UnitPrice = l.UnitPrice;
                existing.VatRate = l.VatRate;
                existing.Total = lineNet;
                existing.UpdatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                order.Lines.Add(new OrderLine
                {
                    ItemId = l.ItemId,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate,
                    Total = lineNet
                });
            }
        }

        order.TotalNet = totalNet;
        order.TotalVat = totalVat;
        order.TotalGross = totalNet + totalVat;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("Sipariş başka bir kullanıcı tarafından değiştirildi.");
        }

        return new OrderDetailDto(
            order.Id,
            order.BranchId,
            order.OrderNumber,
            order.ContactId,
            order.Contact.Name,
            order.DateUtc,
            order.Status,
            order.TotalNet,
            order.TotalVat,
            order.TotalGross,
            order.Currency,
            order.Description,
            order.Lines.Select(x => new OrderLineDto(x.Id, x.ItemId, null, x.Description, x.Quantity, x.UnitPrice, x.VatRate, x.Total)).ToList(),
            Convert.ToBase64String(order.RowVersion),
            order.CreatedAtUtc,
            order.UpdatedAtUtc
        );
    }
}
