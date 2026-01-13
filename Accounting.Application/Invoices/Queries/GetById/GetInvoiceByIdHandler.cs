using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Utils; // Money.S2/S3/S4
using Accounting.Application.Invoices.Queries.Dto;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Invoices.Queries.GetById;

public class GetInvoiceByIdHandler : IRequestHandler<GetInvoiceByIdQuery, InvoiceDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    
    public GetInvoiceByIdHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<InvoiceDto> Handle(GetInvoiceByIdQuery q, CancellationToken cancellationToken)
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
                l.ExpenseDefinitionId, // Added
                l.ItemCode,   // snapshot
                l.ItemName,   // snapshot
                l.Unit,       // snapshot
                Money.S3(l.Qty),
                Money.S4(l.UnitPrice),
                l.VatRate,
                Money.S2(l.DiscountRate),   // Added
                Money.S2(l.DiscountAmount), // Added
                Money.S2(l.Net),
                Money.S2(l.Vat),
                l.WithholdingRate,          // Added
                Money.S2(l.WithholdingAmount), // Added
                Money.S2(l.Gross),
                Money.S2(l.GrandTotal)      // Added
            ))
            .ToList();

        return new InvoiceDto(
            inv.Id,
            inv.ContactId,
            inv.Contact.Code,
            inv.Contact.Name,
            inv.DateUtc,
            inv.InvoiceNumber,
            inv.Currency,
            Money.S2(inv.TotalLineGross),
            Money.S2(inv.TotalDiscount),
            Money.S2(inv.TotalNet),
            Money.S2(inv.TotalVat),
            Money.S2(inv.TotalWithholding),
            Money.S2(inv.TotalGross),
            Money.S2(inv.Balance),
            lines,
            Convert.ToBase64String(inv.RowVersion),
            inv.CreatedAtUtc,
            inv.UpdatedAtUtc,
            (int)inv.Type,
            inv.BranchId,
            inv.Branch.Code,
            inv.Branch.Name,
            inv.WaybillNumber,
            inv.WaybillDateUtc,
            inv.PaymentDueDateUtc
        );
    }
}
