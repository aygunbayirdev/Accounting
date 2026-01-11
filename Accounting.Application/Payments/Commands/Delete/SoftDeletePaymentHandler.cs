using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions; // ApplyBranchFilter
using Accounting.Application.Common.Interfaces; // ICurrentUserService
using Accounting.Application.Services;
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Accounting.Application.Payments.Commands.Delete;

namespace Accounting.Application.Payments.Commands.Delete;

public class SoftDeletePaymentHandler : IRequestHandler<SoftDeletePaymentCommand>
{
    private readonly IAppDbContext _db;
    private readonly IInvoiceBalanceService _balanceService;
    private readonly IAccountBalanceService _accountBalanceService;
    private readonly ICurrentUserService _currentUserService;

    public SoftDeletePaymentHandler(
        IAppDbContext db,
        IInvoiceBalanceService balanceService,
        IAccountBalanceService accountBalanceService,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _balanceService = balanceService;
        _accountBalanceService = accountBalanceService;
        _currentUserService = currentUserService;
    }

    public async Task Handle(SoftDeletePaymentCommand req, CancellationToken ct)
    {
        var p = await _db.Payments
            .ApplyBranchFilter(_currentUserService)
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (p is null) throw new NotFoundException("Payment", req.Id);

        byte[] originalBytes;
        try
        {
            originalBytes = Convert.FromBase64String(req.RowVersion);
        }
        catch (FormatException)
        {
            throw new FluentValidation.ValidationException("RowVersion is not valid Base64.");
        }

        _db.Entry(p).Property(nameof(Payment.RowVersion)).OriginalValue = originalBytes;

        p.IsDeleted = true;
        p.DeletedAtUtc = DateTime.UtcNow;
        p.UpdatedAtUtc = DateTime.UtcNow;

        // LinkedInvoiceId'yi sakla (SaveChanges sonrası kullanacağız)
        var linkedInvoiceId = p.LinkedInvoiceId;

        // Transaction: Payment delete + Invoice Balance birlikte commit
        await using var tx = await _db.BeginTransactionAsync(ct);
        try
        {
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConcurrencyConflictException("Ödeme başka biri tarafından güncellendi/silindi.");
            }

            if (linkedInvoiceId.HasValue)
            {
                await _balanceService.RecalculateBalanceAsync(linkedInvoiceId.Value, ct);
                await _db.SaveChangesAsync(ct);
            }

            // Account Balance Update
            if (p.AccountId > 0)
            {
                await _accountBalanceService.RecalculateBalanceAsync(p.AccountId, ct);
                await _db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
