using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Utils; // Money.S2/S3/S4
using Accounting.Application.Invoices.Queries.Dto;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Invoices.Queries.GetById;

public class GetInvoiceByIdHandler : IRequestHandler<GetInvoiceByIdQuery, InvoiceDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetInvoiceByIdHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<InvoiceDetailDto> Handle(GetInvoiceByIdQuery q, CancellationToken cancellationToken)
    {
        var inv = await _db.Invoices
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService)
            .Include(i => i.Contact)
            .Include(i => i.Branch)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == q.Id, cancellationToken);

        if (inv is null)
            throw new NotFoundException("Invoice", q.Id);

        var lines = inv.Lines
            .OrderBy(l => l.Id)
            .Select(l => new InvoiceLineDto(
                l.Id,
                l.ItemId,
                l.ItemCode,   // snapshot
                l.ItemName,   // snapshot
                l.Unit,       // snapshot
                l.Qty,
                l.UnitPrice,
                l.VatRate,
                l.DiscountRate,
                l.DiscountAmount,
                l.Net,
                l.Vat,
                l.WithholdingRate,
                l.WithholdingAmount,
                l.Gross,
                l.GrandTotal
            ))
            .ToList();

        return new InvoiceDetailDto(
            inv.Id,
            inv.ContactId,
            inv.Contact.Code,
            inv.Contact.Name,
            inv.DateUtc,
            inv.InvoiceNumber,
            inv.Currency,
            inv.TotalLineGross,
            inv.TotalDiscount,
            inv.TotalNet,
            inv.TotalVat,
            inv.TotalWithholding,
            inv.TotalGross,
            inv.Balance,
            lines,
            (int)inv.Type,
            (int)inv.DocumentType,
            inv.BranchId,
            inv.Branch.Code,
            inv.Branch.Name,
            inv.WaybillNumber,
            inv.WaybillDateUtc,
            inv.PaymentDueDateUtc,
            Convert.ToBase64String(inv.RowVersion),
            inv.CreatedAtUtc,
            inv.UpdatedAtUtc
        );
    }
}
