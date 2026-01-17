using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Orders.Dto;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Orders.Queries.GetById;

public class GetOrderByIdHandler : IRequestHandler<GetOrderByIdQuery, OrderDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetOrderByIdHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<OrderDetailDto> Handle(GetOrderByIdQuery r, CancellationToken ct)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
                .ThenInclude(l => l.Item)
            .Include(o => o.Contact)
            .ApplyBranchFilter(_currentUserService)
            .FirstOrDefaultAsync(o => o.Id == r.Id, ct);

        if (order is null)
            throw new NotFoundException("Order", r.Id);

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
            order.Lines.Select(l => new OrderLineDto(
                l.Id,
                l.ItemId,
                l.Item?.Name,
                l.Description,
                l.Quantity,
                l.UnitPrice,
                l.VatRate,
                l.Total
            )).ToList(),
            Convert.ToBase64String(order.RowVersion),
            order.CreatedAtUtc,
            order.UpdatedAtUtc
        );
    }
}
