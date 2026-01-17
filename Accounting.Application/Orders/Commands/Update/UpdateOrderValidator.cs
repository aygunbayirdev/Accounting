using Accounting.Application.Common.Abstractions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Orders.Commands.Update;

public class UpdateOrderValidator : AbstractValidator<UpdateOrderCommand>
{
    private readonly IAppDbContext _db;

    public UpdateOrderValidator(IAppDbContext db)
    {
        _db = db;

        RuleFor(x => x.Id)
            .GreaterThan(0);

        RuleFor(x => x.ContactId)
            .GreaterThan(0)
            .MustAsync(ContactExistsAsync).WithMessage("Cari bulunamadı.");

        RuleFor(x => x.DateUtc)
            .NotEmpty();

        RuleFor(x => x.Description)
            .MaximumLength(200);

        RuleFor(x => x.RowVersion)
            .NotEmpty();

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("En az bir sipariş kalemi gereklidir.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description)
                .NotEmpty()
                .MaximumLength(200);
        });
    }

    private async Task<bool> ContactExistsAsync(int contactId, CancellationToken ct)
    {
        return await _db.Contacts.AnyAsync(c => c.Id == contactId, ct);
    }
}
