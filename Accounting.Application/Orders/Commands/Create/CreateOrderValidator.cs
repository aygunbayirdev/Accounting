using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Interfaces;
using Accounting.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Orders.Commands.Create;

public class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public CreateOrderValidator(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;

        // BranchId check removed

        RuleFor(x => x.ContactId)
            .GreaterThan(0)
            .MustAsync(ContactIsValidForBranchAsync).WithMessage("Cari bulunamadı veya bu şubeye ait değil.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .Must(t => t != InvoiceType.Expense)
            .WithMessage("Masraf faturası için sipariş oluşturulamaz.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .MaximumLength(3);

        RuleFor(x => x.DateUtc)
            .NotEmpty();

        RuleFor(x => x.Description)
            .MaximumLength(200);

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("En az bir sipariş kalemi gereklidir.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description)
                .NotEmpty()
                .MaximumLength(200);

            line.RuleFor(l => l.VatRate)
                .Must(v => v == 0 || v == 1 || v == 10 || v == 20)
                .WithMessage("KDV oranı 0, 1, 10 veya 20 olmalıdır.");
        });
    }

    private async Task<bool> ContactIsValidForBranchAsync(int contactId, CancellationToken ct)
    {
        if (!_currentUserService.BranchId.HasValue) return false;
        var currentBranchId = _currentUserService.BranchId.Value;

        var contact = await _db.Contacts
            .AsNoTracking()
            .Where(c => c.Id == contactId && !c.IsDeleted)
            .Select(c => new { c.BranchId })
            .FirstOrDefaultAsync(ct);
        
        if (contact == null) return false;

        return contact.BranchId == currentBranchId;
    }
}
