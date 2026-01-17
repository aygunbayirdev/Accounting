using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Validation;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using Accounting.Application.Common.Interfaces;

namespace Accounting.Application.Invoices.Commands.Update
{
    public sealed class UpdateInvoiceValidator : AbstractValidator<UpdateInvoiceCommand>
    {
        private readonly IAppDbContext _db;
        private readonly ICurrentUserService _currentUserService;

        public UpdateInvoiceValidator(IAppDbContext db, ICurrentUserService currentUserService)
        {
            _db = db;
            _currentUserService = currentUserService;

            RuleFor(x => x.Id).GreaterThan(0);
            // RuleFor(x => x.BranchId).GreaterThan(0); // Removed

            RuleFor(x => x.RowVersionBase64).MustBeValidRowVersion();  // Extension

            RuleFor(x => x.DateUtc)
                .Must(d => d.Kind == DateTimeKind.Utc || d.Kind == DateTimeKind.Unspecified)
                .WithMessage("DateUtc geçerli bir tarih olmalıdır.");

            RuleFor(x => x.Currency).MustBeValidCurrency();            // Extension

            RuleFor(x => x.ContactId).GreaterThan(0);

            RuleFor(x => x.Lines)
                .NotNull().WithMessage("Lines null olamaz.")
                .Must(l => l != null && l.Count > 0).WithMessage("En az bir satır olmalıdır.");

            RuleFor(x => x.Type)
                .IsInEnum()
                .WithMessage("Geçersiz fatura türü.");

            // ✅ Branch Tutarlılık Kontrolü: Contact aynı şubeye ait olmalı
            RuleFor(x => x)
                .MustAsync(ContactBelongsToSameBranchAsync)
                .WithMessage("Cari (Contact) fatura ile aynı şubeye ait olmalıdır.");

            // ✅ Branch Tutarlılık Kontrolü: Item'lar aynı şubeye ait olmalı
            RuleFor(x => x)
                .MustAsync(AllItemsBelongToSameBranchAsync)
                .WithMessage("Fatura satırlarındaki ürünler (Item) fatura ile aynı şubeye ait olmalıdır.");

            // Id>0 olan satırlarda tekrar kontrolü
            RuleFor(x => x.Lines)
                .Must(lines =>
                {
                    var ids = lines.Where(l => l.Id > 0).Select(l => l.Id);
                    return ids.Distinct().Count() == ids.Count();
                })
                .WithMessage("Lines içinde tekrar eden satır Id değerleri var.");

            RuleForEach(x => x.Lines).SetValidator(new UpdateInvoiceLineValidator());
        }

        private async Task<bool> ContactBelongsToSameBranchAsync(UpdateInvoiceCommand cmd, CancellationToken ct)
        {
            if (!_currentUserService.BranchId.HasValue) return false;
            var currentBranchId = _currentUserService.BranchId.Value;

            var contact = await _db.Contacts
                .AsNoTracking()
                .Where(c => c.Id == cmd.ContactId && !c.IsDeleted)
                .Select(c => new { c.BranchId })
                .FirstOrDefaultAsync(ct);

            if (contact == null)
                return false;

            return contact.BranchId == currentBranchId;
        }

        private async Task<bool> AllItemsBelongToSameBranchAsync(UpdateInvoiceCommand cmd, CancellationToken ct)
        {
            if (!_currentUserService.BranchId.HasValue) return false;
            var currentBranchId = _currentUserService.BranchId.Value;

            var itemIds = cmd.Lines
                .Where(l => l.ItemId.HasValue)
                .Select(l => l.ItemId!.Value)
                .Distinct()
                .ToList();

            if (!itemIds.Any())
                return true;

            var mismatchedItems = await _db.Items
                .AsNoTracking()
                .Where(i => itemIds.Contains(i.Id) && !i.IsDeleted && i.BranchId != currentBranchId)
                .AnyAsync(ct);

            return !mismatchedItems;
        }
    }

    internal sealed class UpdateInvoiceLineValidator : AbstractValidator<UpdateInvoiceLineDto>
    {
        public UpdateInvoiceLineValidator()
        {
            RuleFor(l => l.Id).GreaterThanOrEqualTo(0);
            RuleFor(l => l.ItemId).GreaterThan(0).When(l => l.ItemId.HasValue);
            RuleFor(l => l.VatRate).InclusiveBetween(0, 100);
        }
    }
}
