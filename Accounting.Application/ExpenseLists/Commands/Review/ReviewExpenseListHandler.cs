using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Utils;
using Accounting.Application.ExpenseLists.Dto;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.ExpenseLists.Commands.Review;

public class ReviewExpenseListHandler : IRequestHandler<ReviewExpenseListCommand, ExpenseListDetailDto>
{
    private readonly IAppDbContext _db;
    public ReviewExpenseListHandler(IAppDbContext db) => _db = db;

    public async Task<ExpenseListDetailDto> Handle(ReviewExpenseListCommand req, CancellationToken ct)
    {
        var list = await _db.ExpenseLists
            .Include(x => x.Lines.Where(l => !l.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct);

        if (list is null)
            throw new NotFoundException("ExpenseList", req.Id);

        if (list.Status != ExpenseListStatus.Draft)
            throw new BusinessRuleException("Only Draft expense lists can be reviewed.");

        if (!list.Lines.Any())
            throw new BusinessRuleException("Expense list must have at least one line to review.");

        list.Status = ExpenseListStatus.Reviewed;
        list.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new ExpenseListDetailDto(
            list.Id,
            list.BranchId,
            list.Name,
            list.Status.ToString(),
            list.Lines.Select(l => new ExpenseLineDto(
                l.Id,
                l.ExpenseListId,
                l.DateUtc,
                l.SupplierId,
                l.Currency,
                l.Amount,
                l.VatRate,
                l.Category,
                l.Notes
            )).ToList(),
            DecimalExtensions.RoundAmount(list.Lines.Sum(x => x.Amount)),
            Convert.ToBase64String(list.RowVersion),
            list.CreatedAtUtc,
            list.UpdatedAtUtc
        );
    }
}