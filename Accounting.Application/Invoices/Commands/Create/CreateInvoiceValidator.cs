using Accounting.Application.Common.Interfaces;
using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Validation;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Accounting.Application.Invoices.Commands.Create;

public class CreateInvoiceValidator : AbstractValidator<CreateInvoiceCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public CreateInvoiceValidator(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;

        // Temel alanlar
        // BranchId check removed (using CurrentUser)

        RuleFor(x => x.ContactId)
            .GreaterThan(0);

        // DateTime .NET tarafından otomatik parse edildi, sadece geçerli tarih olduğunu kontrol et
        RuleFor(x => x.DateUtc)
            .NotEmpty()
            .WithMessage("DateUtc gereklidir.");

        RuleFor(x => x.Currency).MustBeValidCurrency();             // Extension

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Geçersiz fatura türü.");

        // ✅ Branch Tutarlılık Kontrolü: Contact aynı şubeye ait olmalı
        RuleFor(x => x)
            .MustAsync(ContactBelongsToSameBranchAsync)
            .WithMessage("Cari (Contact) fatura ile aynı şubeye ait olmalıdır.")
            .When(x => x.ContactId > 0 && _currentUserService.BranchId.HasValue);

        // Satırlar
        RuleFor(x => x.Lines)
            .NotNull()
            .Must(l => l.Count > 0)
            .WithMessage("En az bir satır girmelisiniz.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId)
                .GreaterThan(0)
                .When(l => l.ItemId.HasValue);

            line.RuleFor(l => l.VatRate)
                .InclusiveBetween(0, 100);
        });

        // ✅ Branch Tutarlılık Kontrolü: Satırlardaki Item'lar aynı şubeye ait olmalı
        RuleFor(x => x)
            .MustAsync(AllItemsBelongToSameBranchAsync)
            .WithMessage("Fatura satırlarındaki ürünler (Item) fatura ile aynı şubeye ait olmalıdır.")
            .When(x => x.Lines != null && x.Lines.Any(l => l.ItemId.HasValue) && _currentUserService.BranchId.HasValue);
    }

    private async Task<bool> ContactBelongsToSameBranchAsync(CreateInvoiceCommand cmd, CancellationToken ct)
    {
        if (!_currentUserService.BranchId.HasValue) return false;
        var currentBranchId = _currentUserService.BranchId.Value;

        var contact = await _db.Contacts
            .AsNoTracking()
            .Where(c => c.Id == cmd.ContactId && !c.IsDeleted)
            .Select(c => new { c.BranchId })
            .FirstOrDefaultAsync(ct);

        if (contact == null)
            return false; // Contact bulunamadı - ayrı validasyonda yakalanabilir

        return contact.BranchId == currentBranchId;
    }

    private async Task<bool> AllItemsBelongToSameBranchAsync(CreateInvoiceCommand cmd, CancellationToken ct)
    {
        if (!_currentUserService.BranchId.HasValue) return false;
        var currentBranchId = _currentUserService.BranchId.Value;

        var itemIds = cmd.Lines
            .Where(l => l.ItemId.HasValue)
            .Select(l => l.ItemId!.Value)
            .Distinct()
            .ToList();

        if (!itemIds.Any())
            return true; // Item yoksa (ExpenseDefinition kullanılıyor olabilir)

        var mismatchedItems = await _db.Items
            .AsNoTracking()
            .Where(i => itemIds.Contains(i.Id) && !i.IsDeleted && i.BranchId != currentBranchId)
            .AnyAsync(ct);

        return !mismatchedItems;
    }
}
