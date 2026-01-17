using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions; // ApplyBranchFilter
using Accounting.Application.Common.Interfaces; // ICurrentUserService
using Accounting.Application.FixedAssets.Queries.Dto;
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.FixedAssets.Commands.Update;

public sealed class UpdateFixedAssetHandler
    : IRequestHandler<UpdateFixedAssetCommand, FixedAssetDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public UpdateFixedAssetHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<FixedAssetDetailDto> Handle(
        UpdateFixedAssetCommand r,
        CancellationToken ct)
    {
        var entity = await _db.FixedAssets
            .ApplyBranchFilter(_currentUserService)
            .FirstOrDefaultAsync(x => x.Id == r.Id, ct);

        if (entity is null)
        {
            throw new NotFoundException("FixedAsset", r.Id);
        }

        if (entity.IsDeleted)
        {
            throw new InvalidOperationException("Cannot update a deleted fixed asset.");
        }

        // Concurrency: RowVersion
        if (string.IsNullOrWhiteSpace(r.RowVersionBase64))
        {
            throw new BusinessRuleException("RowVersion is required.");
        }

        var originalRowVersion = Convert.FromBase64String(r.RowVersionBase64);
        _db.Entry(entity).Property(nameof(FixedAsset.RowVersion)).OriginalValue = originalRowVersion;

        // Code unique mi? (kendisi hariç)
        var codeExists = await _db.FixedAssets
            .AnyAsync(x => x.Id != r.Id && x.Code == r.Code && !x.IsDeleted, ct);

        if (codeExists)
        {
            throw new BusinessRuleException("A fixed asset with the same code already exists.");
        }

        if (r.UsefulLifeYears <= 0)
        {
            throw new BusinessRuleException("Useful life must be greater than zero.");
        }

        decimal depRate =
            Math.Round(100m / r.UsefulLifeYears, 4, MidpointRounding.AwayFromZero);

        var now = DateTime.UtcNow;

        entity.Code = r.Code.Trim();
        entity.Name = r.Name.Trim();
        entity.PurchaseDateUtc = r.PurchaseDateUtc;
        entity.PurchasePrice = r.PurchasePrice;
        entity.UsefulLifeYears = r.UsefulLifeYears;
        entity.DepreciationRatePercent = depRate;
        entity.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);

        // Fresh read (AsNoTracking) → DTO
        var fresh = await _db.FixedAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == r.Id, ct);

        if (fresh is null)
        {
            throw new NotFoundException("FixedAsset", r.Id);
        }

        return new FixedAssetDetailDto(
            fresh.Id,
            fresh.Code,
            fresh.Name,
            fresh.PurchaseDateUtc,
            fresh.PurchasePrice,
            fresh.UsefulLifeYears,
            fresh.DepreciationRatePercent,
            fresh.IsDeleted,
            Convert.ToBase64String(fresh.RowVersion),
            fresh.CreatedAtUtc,
            fresh.UpdatedAtUtc
        );
    }
}
