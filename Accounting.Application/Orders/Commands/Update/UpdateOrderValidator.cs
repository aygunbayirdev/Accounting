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

            line.RuleFor(l => l.Quantity)
                .GreaterThan(0)
                .WithMessage("Miktar 0'dan büyük olmalıdır.");

            line.RuleFor(l => l.UnitPrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Birim fiyat 0'dan küçük olamaz.");

            line.RuleFor(l => l.VatRate)
                .Must(v => v == 0 || v == 1 || v == 10 || v == 20)
                .WithMessage("KDV oranı 0, 1, 10 veya 20 olmalıdır.");
        });
    }

    private async Task<bool> ContactExistsAsync(int contactId, CancellationToken ct)
    {
        return await _db.Contacts.AnyAsync(c => c.Id == contactId, ct);
    }
}
