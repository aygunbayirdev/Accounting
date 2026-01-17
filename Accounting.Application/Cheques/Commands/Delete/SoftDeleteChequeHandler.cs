using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Cheques.Commands.Delete;

public class SoftDeleteChequeHandler : IRequestHandler<SoftDeleteChequeCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public SoftDeleteChequeHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task Handle(SoftDeleteChequeCommand request, CancellationToken ct)
    {
        var cheque = await _db.Cheques
            .ApplyBranchFilter(_currentUserService)
            .FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, ct);

        if (cheque is null)
            throw new NotFoundException("Cheque", request.Id);

        // Optimistic Concurrency Check
        var requestedRowVersion = Convert.FromBase64String(request.RowVersionBase64);
        if (!cheque.RowVersion.SequenceEqual(requestedRowVersion))
            throw new DbUpdateConcurrencyException(
                "Bu çek/senet baþka bir kullanýcý tarafýndan güncellenmiþ. Lütfen sayfayý yenileyip tekrar deneyin.");

        // Prevent deletion if paid or bounced
        if (cheque.Status == ChequeStatus.Paid || cheque.Status == ChequeStatus.Bounced)
            throw new FluentValidation.ValidationException("Cannot delete paid or bounced cheque");

        cheque.IsDeleted = true;
        cheque.DeletedAtUtc = DateTime.UtcNow;
        // RowVersion otomatik güncellenir (EF Core tarafýndan)

        await _db.SaveChangesAsync(ct);
    }
}
