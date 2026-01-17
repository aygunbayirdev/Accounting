using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Utils;
using Accounting.Application.Payments.Queries.Dto;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Accounting.Application.Payments.Queries.GetById;

public class GetPaymentByIdHandler : IRequestHandler<GetPaymentByIdQuery, PaymentDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetPaymentByIdHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<PaymentDetailDto> Handle(GetPaymentByIdQuery q, CancellationToken ct)
    {
        var p = await _db.Payments
            .AsNoTracking()
            .ApplyBranchFilter(_currentUserService)
            .FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        if (p is null) throw new NotFoundException("Payment", q.Id);

        var inv = CultureInfo.InvariantCulture;

        return new PaymentDetailDto(
            p.Id,
            p.AccountId,
            p.ContactId,
            p.LinkedInvoiceId,
            p.DateUtc,
            p.Direction.ToString(),
            Money.S2(p.Amount),
            p.Currency,
            p.Description,
            Convert.ToBase64String(p.RowVersion),
            p.CreatedAtUtc,
            p.UpdatedAtUtc
        );
    }
}
