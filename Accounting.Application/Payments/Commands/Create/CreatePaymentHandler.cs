using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Utils;
using Accounting.Application.Common.Validation;
using Accounting.Application.Services;
using Accounting.Domain.Entities;
using MediatR;
using System.Globalization;

using Accounting.Application.Common.Interfaces;

namespace Accounting.Application.Payments.Commands.Create;

public class CreatePaymentHandler : IRequestHandler<CreatePaymentCommand, CreatePaymentResult>
{
    private readonly IAppDbContext _db;
    private readonly IInvoiceBalanceService _balanceService;
    private readonly IAccountBalanceService _accountBalanceService;
    private readonly ICurrentUserService _currentUserService;

    public CreatePaymentHandler(IAppDbContext db, IInvoiceBalanceService balanceService, IAccountBalanceService accountBalanceService, ICurrentUserService currentUserService)
    {
        _db = db;
        _balanceService = balanceService;
        _accountBalanceService = accountBalanceService;
        _currentUserService = currentUserService;
    }

    public async Task<CreatePaymentResult> Handle(CreatePaymentCommand req, CancellationToken ct)
    {
        // Amount Parse
        if (!Money.TryParse2(req.Amount, out var amount))
            throw new FluentValidation.ValidationException("Amount is invalid.");

        // Currency Normalization & Validation
        var currency = CommonValidationRules.NormalizeAndValidateCurrency(req.Currency);

        var branchId = _currentUserService.BranchId ?? throw new UnauthorizedAccessException();

        var entity = new Payment
        {
            BranchId = branchId,
            AccountId = req.AccountId,
            ContactId = req.ContactId,
            LinkedInvoiceId = req.LinkedInvoiceId,
            DateUtc = DateTime.SpecifyKind(req.DateUtc, DateTimeKind.Utc),
            Direction = req.Direction,
            Amount = amount,
            Currency = currency,
            Description = req.Description?.Trim()
        };

        // Transaction: Payment + Invoice Balance birlikte commit
        await using var tx = await _db.BeginTransactionAsync(ct);
        try
        {
            _db.Payments.Add(entity);
            await _db.SaveChangesAsync(ct);

            if (entity.LinkedInvoiceId.HasValue)
            {
                await _balanceService.RecalculateBalanceAsync(entity.LinkedInvoiceId.Value, ct);
                await _db.SaveChangesAsync(ct);
            }

            // Account Balance Update
            if (entity.AccountId > 0)
            {
                await _accountBalanceService.RecalculateBalanceAsync(entity.AccountId, ct);
                await _db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        var inv = CultureInfo.InvariantCulture;
        return new CreatePaymentResult(
            entity.Id,
            Money.S2(entity.Amount),
            entity.Currency
        );
    }
}
