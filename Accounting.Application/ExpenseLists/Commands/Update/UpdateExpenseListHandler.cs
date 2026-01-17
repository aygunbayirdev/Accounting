using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions; // ApplyBranchFilter
using Accounting.Application.Common.Interfaces; // ICurrentUserService
using Accounting.Application.Common.Utils;
using Accounting.Application.ExpenseLists.Dto;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Accounting.Application.ExpenseLists.Commands.Update;

public class UpdateExpenseListHandler : IRequestHandler<UpdateExpenseListCommand, ExpenseListDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public UpdateExpenseListHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<ExpenseListDetailDto> Handle(UpdateExpenseListCommand req, CancellationToken ct)
    {
        var list = await _db.ExpenseLists
            .ApplyBranchFilter(_currentUserService)
            .Include(x => x.Lines.Where(l => !l.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct);

        if (list is null)
            throw new NotFoundException("ExpenseList", req.Id);

        // Only Draft lists can be updated
        if (list.Status != ExpenseListStatus.Draft)
            throw new BusinessRuleException("Only Draft expense lists can be updated.");

        // Concurrency check
        byte[] originalBytes;
        try
        {
            originalBytes = Convert.FromBase64String(req.RowVersion);
        }
        catch (FormatException)
        {
            throw new FluentValidation.ValidationException("RowVersion is not valid Base64.");
        }
        _db.Entry(list).Property(nameof(ExpenseList.RowVersion)).OriginalValue = originalBytes;

        // Update name
        list.Name = string.IsNullOrWhiteSpace(req.Name) ? "Masraf Listesi" : req.Name.Trim();
        list.UpdatedAtUtc = DateTime.UtcNow;

        // ✅ DIFFERENTIAL UPDATE LOGIC

        // 1. Mevcut line ID'leri
        var existingLineIds = list.Lines.Select(l => l.Id).ToHashSet();

        // 2. Gelen line ID'leri (yeni olmayanlar)
        var incomingLineIds = req.Lines
            .Where(l => l.Id.HasValue)
            .Select(l => l.Id!.Value)
            .ToHashSet();

        // 3. Silinecek line'lar (mevcut - gelen)
        var lineIdsToDelete = existingLineIds.Except(incomingLineIds);
        foreach (var lineId in lineIdsToDelete)
        {
            var line = list.Lines.First(l => l.Id == lineId);
            line.IsDeleted = true;
            line.DeletedAtUtc = DateTime.UtcNow;
        }

        // 4. Yeni ve güncellenecek line'lar
        foreach (var lineDto in req.Lines)
        {
            if (!Money.TryParse2(lineDto.Amount, out var amount))
                throw new FluentValidation.ValidationException("Amount is invalid.");

            var dateUtc = DateTime.SpecifyKind(lineDto.DateUtc, DateTimeKind.Utc);

            if (lineDto.Id.HasValue)
            {
                // ✅ GÜNCELLE (mevcut line)
                var existingLine = list.Lines.FirstOrDefault(l => l.Id == lineDto.Id.Value);
                if (existingLine is null)
                    throw new NotFoundException("ExpenseLine", lineDto.Id.Value);

                existingLine.DateUtc = dateUtc;
                existingLine.SupplierId = lineDto.SupplierId;
                existingLine.Currency = lineDto.Currency.ToUpperInvariant();
                existingLine.Amount = amount;
                existingLine.VatRate = lineDto.VatRate;
                existingLine.Category = lineDto.Category?.Trim();
                existingLine.Notes = lineDto.Notes?.Trim();
                existingLine.UpdatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                // YENİ EKLE
                var newLine = new ExpenseLine
                {
                    ExpenseListId = list.Id,
                    DateUtc = dateUtc,
                    SupplierId = lineDto.SupplierId,
                    Currency = lineDto.Currency.ToUpperInvariant(),
                    Amount = amount,
                    VatRate = lineDto.VatRate,
                    Category = lineDto.Category?.Trim(),
                    Notes = lineDto.Notes?.Trim(),
                    CreatedAtUtc = DateTime.UtcNow
                };

                list.Lines.Add(newLine);
            }
        }

        // Save
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                "Kayıt başka bir kullanıcı tarafından güncellendi. Lütfen sayfayı yenileyip tekrar deneyin.");
        }

        // Fresh read for response
        var fresh = await _db.ExpenseLists
            .Include(x => x.Lines.Where(l => !l.IsDeleted))
            .AsNoTracking()
            .FirstAsync(x => x.Id == req.Id, ct);

        return new ExpenseListDetailDto(
            Id: fresh.Id,
            BranchId: fresh.BranchId,
            Name: fresh.Name,
            Status: fresh.Status.ToString(),
            Lines: fresh.Lines.Select(l => new ExpenseLineDto(
                Id: l.Id,
                ExpenseListId: fresh.Id,
                DateUtc: l.DateUtc,
                SupplierId: l.SupplierId,
                Currency: l.Currency,
                Amount: Money.S2(l.Amount),
                VatRate: l.VatRate,
                Category: l.Category,
                Notes: l.Notes
            )).ToList(),
            TotalAmount: Money.S2(fresh.Lines.Sum(l => l.Amount)),
            CreatedAtUtc: fresh.CreatedAtUtc,
            UpdatedAtUtc: fresh.UpdatedAtUtc,
            RowVersion: Convert.ToBase64String(fresh.RowVersion)
        );
    }
}