using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Services;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Cheques.Commands.UpdateStatus;

public class UpdateChequeStatusHandler : IRequestHandler<UpdateChequeStatusCommand>
{
    private readonly IAppDbContext _db;
    private readonly IAccountBalanceService _accountBalanceService;

    public UpdateChequeStatusHandler(IAppDbContext db, IAccountBalanceService accountBalanceService)
    {
        _db = db;
        _accountBalanceService = accountBalanceService;
    }

    public async Task Handle(UpdateChequeStatusCommand request, CancellationToken ct)
    {
        var cheque = await _db.Cheques
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted, ct);

        if (cheque == null)
            throw new NotFoundException("Cheque", request.Id);

        // Optimistic Concurrency Check
        var requestedRowVersion = Convert.FromBase64String(request.RowVersionBase64);
        if (!cheque.RowVersion.SequenceEqual(requestedRowVersion))
            throw new DbUpdateConcurrencyException(
                "Bu çek/senet başka bir kullanıcı tarafından güncellenmiş. Lütfen sayfayı yenileyip tekrar deneyin.");

        // Status Validations
        if (cheque.Status == ChequeStatus.Paid)
            throw new BusinessRuleException("Zaten ödenmiş/tahsil edilmiş evrakın durumu değiştirilemez.");

        if (cheque.Status == request.NewStatus)
            return; // No change

        // Workflow Logic
        if (request.NewStatus == ChequeStatus.Paid)
        {
            if (!request.CashBankAccountId.HasValue)
                throw new BusinessRuleException("Tahsilat/Ödeme işlemi için Kasa/Banka hesabı seçilmelidir.");

            // Transaction: Payment + AccountBalance + ChequeStatus birlikte commit
            await using var tx = await _db.BeginTransactionAsync(ct);
            try
            {
                // 1. Payment oluştur
                var payment = CreatePaymentFromCheque(cheque, request.CashBankAccountId.Value, request.TransactionDate ?? DateTime.UtcNow);
                _db.Payments.Add(payment);
                await _db.SaveChangesAsync(ct);

                // 2. Account Balance güncelle
                await _accountBalanceService.RecalculateBalanceAsync(request.CashBankAccountId.Value, ct);
                await _db.SaveChangesAsync(ct);

                // 3. Cheque status güncelle
                cheque.Status = request.NewStatus;
                cheque.UpdatedAtUtc = DateTime.UtcNow;
                // RowVersion otomatik güncellenir (EF Core tarafından)
                await _db.SaveChangesAsync(ct);

                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
        else
        {
            // Bounced veya diğer durumlar - sadece status değişikliği
            cheque.Status = request.NewStatus;
            cheque.UpdatedAtUtc = DateTime.UtcNow;
            // RowVersion otomatik güncellenir (EF Core tarafından)
            await _db.SaveChangesAsync(ct);
        }
    }

    private static Payment CreatePaymentFromCheque(Cheque cheque, int accountId, DateTime dateUtc)
    {
        var direction = cheque.Direction == ChequeDirection.Inbound
            ? PaymentDirection.In  // Müşteri çeki tahsilatı -> Kasa Giriş
            : PaymentDirection.Out; // Kendi çekimiz ödenmesi -> Kasa Çıkış

        return new Payment
        {
            BranchId = cheque.BranchId,
            AccountId = accountId,
            ContactId = cheque.ContactId,
            ChequeId = cheque.Id, // Çek-Ödeme ilişkisi
            Direction = direction,
            Amount = cheque.Amount,
            Currency = cheque.Currency,
            DateUtc = dateUtc
        };
    }
}
