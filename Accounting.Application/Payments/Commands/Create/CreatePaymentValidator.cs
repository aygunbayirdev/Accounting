using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Payments.Commands.Create;

public class CreatePaymentValidator : AbstractValidator<CreatePaymentCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public CreatePaymentValidator(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;

        RuleFor(x => x.AccountId)
            .GreaterThan(0)
            .MustAsync(AccountBelongsToBranchAsync).WithMessage("Kasa/Banka hesabı bulunamadı veya bu şubeye ait değil.");

        RuleFor(x => x.Direction).IsInEnum();

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Tutar 0'dan büyük olmalıdır.");

        // CommonValidationRules
        RuleFor(x => x.Currency).MustBeValidCurrency();
        RuleFor(x => x.DateUtc).NotEmpty().WithMessage("DateUtc gereklidir.");

        // LinkedInvoiceId validation
        When(x => x.LinkedInvoiceId.HasValue, () =>
        {
            // 1. Invoice exist check & Branch Check
            RuleFor(x => x.LinkedInvoiceId!.Value)
                .MustAsync(InvoiceBelongsToBranchAsync)
                .WithMessage("Linked invoice not found, deleted, or belongs to another branch.");

            // 2. Currency match
            RuleFor(x => x)
                .MustAsync(async (cmd, ct) =>
                {
                    if (!cmd.LinkedInvoiceId.HasValue) return true;

                    var invoice = await _db.Invoices
                        .AsNoTracking()
                        .Where(i => i.Id == cmd.LinkedInvoiceId.Value)
                        .Select(i => new { i.Currency })
                        .FirstOrDefaultAsync(ct);

                    if (invoice == null) return true;

                    var paymentCurrency = (cmd.Currency ?? "TRY").ToUpperInvariant();
                    return invoice.Currency.ToUpperInvariant() == paymentCurrency;
                })
                .WithMessage("Payment currency must match invoice currency.");

            // 3. Amount <= Balance
            RuleFor(x => x)
                .MustAsync(async (cmd, ct) =>
                {
                    if (!cmd.LinkedInvoiceId.HasValue) return true;

                    var invoice = await _db.Invoices
                        .AsNoTracking()
                        .Where(i => i.Id == cmd.LinkedInvoiceId.Value)
                        .Select(i => new { i.Balance })
                        .FirstOrDefaultAsync(ct);

                    if (invoice == null) return true;

                    return cmd.Amount <= invoice.Balance;
                })
                .WithMessage("Payment amount exceeds invoice balance.");
        });
    }

    private async Task<bool> AccountBelongsToBranchAsync(int accountId, CancellationToken ct)
    {
        if (!_currentUserService.BranchId.HasValue) return false;
        var currentBranchId = _currentUserService.BranchId.Value;

        var account = await _db.CashBankAccounts
            .AsNoTracking()
            .Where(a => a.Id == accountId && !a.IsDeleted)
            .Select(a => new { a.BranchId })
            .FirstOrDefaultAsync(ct);

        return account != null && account.BranchId == currentBranchId;
    }

    private async Task<bool> InvoiceBelongsToBranchAsync(int invoiceId, CancellationToken ct)
    {
        if (!_currentUserService.BranchId.HasValue) return false;
        var currentBranchId = _currentUserService.BranchId.Value;

        var invoice = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId && !i.IsDeleted)
            .Select(i => new { i.BranchId })
            .FirstOrDefaultAsync(ct);

        return invoice != null && invoice.BranchId == currentBranchId;
    }
}
