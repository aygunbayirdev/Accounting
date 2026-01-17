using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Utils;
using Accounting.Application.ExpenseLists.Dto;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using System.Globalization;

namespace Accounting.Application.ExpenseLists.Commands.Create;

public class CreateExpenseListHandler : IRequestHandler<CreateExpenseListCommand, ExpenseListDetailDto>
{
    private readonly IAppDbContext _db;
    public CreateExpenseListHandler(IAppDbContext db) => _db = db;

    public async Task<ExpenseListDetailDto> Handle(CreateExpenseListCommand req, CancellationToken ct)
    {
        var entity = new ExpenseList
        {
            BranchId = req.BranchId,
            Name = string.IsNullOrWhiteSpace(req.Name) ? "Masraf Listesi" : req.Name.Trim(),
            Status = ExpenseListStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            Lines = new List<ExpenseLine>()
        };

        // Lines ekle
        foreach (var lineDto in req.Lines)
        {
            if (!Money.TryParse2(lineDto.Amount, out var amount))
                throw new FluentValidation.ValidationException("Amount is invalid.");

            var line = new ExpenseLine
            {
                DateUtc = DateTime.SpecifyKind(lineDto.DateUtc, DateTimeKind.Utc),
                SupplierId = lineDto.SupplierId,
                Currency = lineDto.Currency.ToUpperInvariant(),
                Amount = amount,
                VatRate = lineDto.VatRate,
                Category = lineDto.Category?.Trim(),
                Notes = lineDto.Notes?.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            entity.Lines.Add(line);
        }

        _db.ExpenseLists.Add(entity);
        await _db.SaveChangesAsync(ct);

        // Return detail DTO
        return new ExpenseListDetailDto(
            Id: entity.Id,
            BranchId: entity.BranchId,
            Name: entity.Name,
            Status: entity.Status.ToString(),
            Lines: entity.Lines.Select(l => new ExpenseLineDto(
                Id: l.Id,
                ExpenseListId: entity.Id,
                DateUtc: l.DateUtc,
                SupplierId: l.SupplierId,
                Currency: l.Currency,
                Amount: Money.S2(l.Amount),
                VatRate: l.VatRate,
                Category: l.Category,
                Notes: l.Notes
            )).ToList(),
            TotalAmount: Money.S2(entity.Lines.Sum(l => l.Amount)),
            CreatedAtUtc: entity.CreatedAtUtc,
            UpdatedAtUtc: entity.UpdatedAtUtc,
            RowVersion: Convert.ToBase64String(entity.RowVersion)
        );
    }
}