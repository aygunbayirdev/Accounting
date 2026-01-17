using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Utils;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Services;

public class AccountBalanceService(IAppDbContext db) : IAccountBalanceService
{
    public async Task<decimal> RecalculateBalanceAsync(int accountId, CancellationToken ct = default)
    {
        // 1. Calculate total In (Giriş) and Out (Çıkış)
        var movements = await db.Payments
            .AsNoTracking()
            .Where(p => p.AccountId == accountId)
            .Select(p => new { p.Direction, p.Amount })
            .ToListAsync(ct);

        var totalIn = movements.Where(m => m.Direction == PaymentDirection.In).Sum(m => m.Amount);
        var totalOut = movements.Where(m => m.Direction == PaymentDirection.Out).Sum(m => m.Amount);

        // 2. Balance = In - Out
        var balance = DecimalExtensions.RoundAmount(totalIn - totalOut);

        // 3. Update Account
        // We need to fetch the account with tracking to update it
        var account = await db.CashBankAccounts.FindAsync(new object[] { accountId }, ct);
        if (account != null)
        {
            account.Balance = balance;
            // Note: SaveChanges is expected to be called by the caller (Handler)
        }

        return balance;
    }
}
