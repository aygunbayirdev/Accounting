using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Utils;
using Accounting.Application.Orders.Dto;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Orders.Commands.Create;

public class CreateOrderHandler(IAppDbContext db, ICurrentUserService currentUserService) : IRequestHandler<CreateOrderCommand, OrderDetailDto>
{
    public async Task<OrderDetailDto> Handle(CreateOrderCommand r, CancellationToken ct)
    {
        var branchId = currentUserService.BranchId ?? throw new UnauthorizedAccessException();

        // 1. Generate Order Number
        var lastOrder = await db.Orders
            .Where(o => o.BranchId == branchId && o.Type == r.Type)
            .OrderByDescending(o => o.OrderNumber)
            .FirstOrDefaultAsync(ct);

        long nextNum = 1;
        if (lastOrder != null && long.TryParse(lastOrder.OrderNumber, out var lastN))
        {
            nextNum = lastN + 1;
        }
        var orderNumber = nextNum.ToString().PadLeft(6, '0');

        // 2. Create Order
        var order = new Order
        {
            BranchId = branchId,
            ContactId = r.ContactId,
            OrderNumber = orderNumber,
            DateUtc = r.DateUtc,
            Type = r.Type,
            Status = OrderStatus.Draft,
            Currency = r.Currency ?? "TRY",
            Description = r.Description,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = []
        };

        decimal totalNet = 0;
        decimal totalVat = 0;

        foreach (var l in r.Lines)
        {
            var lineNet = DecimalExtensions.RoundAmount(l.Quantity * l.UnitPrice);
            var vatAmount = DecimalExtensions.RoundAmount(lineNet * l.VatRate / 100m);

            totalNet += lineNet;
            totalVat += vatAmount;

            order.Lines.Add(new OrderLine
            {
                ItemId = l.ItemId,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                VatRate = l.VatRate,
                Total = lineNet // Storing Net Total line-by-line
            });
        }

        order.TotalNet = totalNet;
        order.TotalVat = totalVat;
        order.TotalGross = totalNet + totalVat;

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // Return DTO
        return new OrderDetailDto(
            order.Id,
            order.BranchId,
            order.OrderNumber,
            order.ContactId,
            "", // Contact name - could be fetched if needed
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
