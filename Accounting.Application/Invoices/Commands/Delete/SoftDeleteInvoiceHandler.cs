using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces; // ICurrentUserService
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Invoices.Commands.Delete;

public class SoftDeleteInvoiceHandler : IRequestHandler<SoftDeleteInvoiceCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public SoftDeleteInvoiceHandler(IAppDbContext db, ICurrentUserService currentUserService) 
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task Handle(SoftDeleteInvoiceCommand req, CancellationToken ct)
    {
        var inv = await _db.Invoices
            .ApplyBranchFilter(_currentUserService)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == req.Id && !i.IsDeleted, ct);

        if (inv is null)
            throw new NotFoundException("Invoice", req.Id);

        // Payment kontrolü
        var hasPayments = await _db.Payments
            .AnyAsync(p => p.LinkedInvoiceId == req.Id && !p.IsDeleted, ct);

        if (hasPayments)
            throw new BusinessRuleException(
                "Cannot delete invoice with linked payments. Please delete payments first.");

        // concurrency
        byte[] originalBytes;
        try
        {
            originalBytes = Convert.FromBase64String(req.RowVersion);
        }
        catch (FormatException)
        {
            throw new FluentValidation.ValidationException("RowVersion is not valid Base64.");
        }
        _db.Entry(inv).Property(nameof(Invoice.RowVersion)).OriginalValue = originalBytes;

        inv.IsDeleted = true;
        inv.DeletedAtUtc = DateTime.UtcNow;
        inv.UpdatedAtUtc = DateTime.UtcNow;

        // InvoiceLines soft delete
        foreach (var line in inv.Lines.Where(l => !l.IsDeleted))
        {
            line.IsDeleted = true;
            line.DeletedAtUtc = DateTime.UtcNow;
        }

        // ---------------------------------------------------------
        // Stok hareketlerini de iptal et (Soft Delete)
        // ---------------------------------------------------------
        var existingMovements = await _db.StockMovements
            .Where(m => m.InvoiceId == inv.Id && !m.IsDeleted)
            .ToListAsync(ct);

        foreach (var move in existingMovements)
        {
            move.IsDeleted = true;
            move.DeletedAtUtc = DateTime.UtcNow;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
        "Kayıt başka bir kullanıcı tarafından güncellendi. Lütfen sayfayı yenileyip tekrar deneyin.");
        }
    }
}