using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Utils;
using Accounting.Application.Invoices.Commands.Create;
using Accounting.Application.Payments.Commands.Create;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Accounting.Application.Common.Interfaces;
using System.Linq;

namespace Accounting.Application.ExpenseLists.Commands.PostToBill;

public class PostExpenseListToBillHandler
    : IRequestHandler<PostExpenseListToBillCommand, PostExpenseListToBillResult>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public PostExpenseListToBillHandler(IAppDbContext db, IMediator mediator, ICurrentUserService currentUserService)
    {
        _db = db;
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    public async Task<PostExpenseListToBillResult> Handle(PostExpenseListToBillCommand req, CancellationToken ct)
    {
        // Liste + satırlar
        var list = await _db.ExpenseLists
            .Include(x => x.Branch)
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == req.ExpenseListId, ct);

        if (list is null)
            throw new NotFoundException("ExpenseList");

        // Security Check: List must belong to current user's branch
        var branchId = _currentUserService.BranchId ?? throw new UnauthorizedAccessException();
        if (list.BranchId != branchId) throw new NotFoundException("ExpenseList");

        if (list.Status != ExpenseListStatus.Reviewed)
            throw new BusinessRuleException("Only Reviewed lists can be posted to bill.");

        // ✅ YENİ: Duplikasyon kontrolü
        if (list.PostedInvoiceId.HasValue)
            throw new BusinessRuleException(
                $"Expense list already posted to invoice #{list.PostedInvoiceId.Value}. Cannot post again.");

        if (!list.Lines.Any())
            throw new InvalidOperationException("Expense list has no lines.");

        // Para birimi bütünlüğü
        var distinctCurrencies = list.Lines.Select(l => l.Currency).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
        if (distinctCurrencies.Count != 1 || !string.Equals(distinctCurrencies[0], req.Currency, StringComparison.InvariantCultureIgnoreCase))
            throw new InvalidOperationException("All expense lines must share the same currency and match the requested currency.");

        // Tedarikçi bütünlüğü
        var nonNullSuppliers = list.Lines.Where(l => l.SupplierId.HasValue).Select(l => l.SupplierId!.Value).Distinct().ToList();
        if (nonNullSuppliers.Count > 1 && nonNullSuppliers.Any(s => s != req.SupplierId))
            throw new InvalidOperationException("Expense lines have multiple suppliers; please normalize before posting.");

        // Fatura tarihi (UTC)
        DateTime dateUtc;
        if (!string.IsNullOrWhiteSpace(req.DateUtc))
        {
            if (!DateTime.TryParse(req.DateUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dateUtc))
                throw new ArgumentException("DateUtc is invalid.");
        }
        else
        {
            dateUtc = DateTime.UtcNow;
        }

        // CreateInvoiceCommand (yeniden kullanım)
        var lines = list.Lines.Select(l => new CreateInvoiceLineDto(
            ItemId: req.ItemId,
            ExpenseDefinitionId: null,
            Qty: "1.000",
            UnitPrice: Money.S2(l.Amount),
            VatRate: l.VatRate,
            DiscountRate: null,
            WithholdingRate: null
        )).ToList();

        var createCmd = new CreateInvoiceCommand(
            ContactId: req.SupplierId,
            DateUtc: dateUtc.ToString("o", CultureInfo.InvariantCulture),
            Currency: req.Currency.ToUpperInvariant(),
            Lines: lines,
            Type: InvoiceType.Purchase.ToString(),
            WaybillNumber: null,
            WaybillDateUtc: null,
            PaymentDueDateUtc: null
        );

        // Transaction: Invoice + Payment + ExpenseList status birlikte commit
        await using var tx = await _db.BeginTransactionAsync(ct);
        try
        {
            var created = await _mediator.Send(createCmd, ct);

            if (req.CreatePayment)
            {
                if (!req.PaymentAccountId.HasValue)
                    throw new BusinessRuleException("PaymentAccountId is required when CreatePayment is true.");

                var paymentCmd = new CreatePaymentCommand(
                    AccountId: req.PaymentAccountId.Value,
                    ContactId: req.SupplierId,
                    LinkedInvoiceId: created.Id,
                    DateUtc: req.PaymentDateUtc ?? dateUtc.ToString("o", CultureInfo.InvariantCulture),
                    Direction: PaymentDirection.Out,
                    Amount: created.TotalGross,
                    Currency: req.Currency.ToUpperInvariant(),
                    Description: null  // Masraf listesinden otomatik ödeme
                );

                await _mediator.Send(paymentCmd, ct);
            }

            // Listeyi "Posted" işaretle ve satırlara InvoiceId yaz
            list.Status = ExpenseListStatus.Posted;
            list.PostedInvoiceId = created.Id;

            foreach (var l in list.Lines)
                l.PostedInvoiceId = created.Id;

            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            return new PostExpenseListToBillResult(
                CreatedInvoiceId: created.Id,
                PostedExpenseCount: list.Lines.Count
            );
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}