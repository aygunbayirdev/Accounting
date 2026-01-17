using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.FixedAssets.Queries.Dto;
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.FixedAssets.Commands.Create;

public sealed class CreateFixedAssetHandler
    : IRequestHandler<CreateFixedAssetCommand, FixedAssetDetailDto>
{
    private readonly IAppDbContext _db;

    public CreateFixedAssetHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<FixedAssetDetailDto> Handle(
        CreateFixedAssetCommand r,
        CancellationToken ct)
    {
        // Code unique mi?
        var codeExists = await _db.FixedAssets
            .AnyAsync(x => x.Code == r.Code && !x.IsDeleted, ct);

        if (codeExists)
        {
            throw new BusinessRuleException("A fixed asset with the same code already exists.");
        }

        // Amortisman oranı: 100 / UsefulLifeYears
        if (r.UsefulLifeYears <= 0)
        {
            throw new BusinessRuleException("Useful life must be greater than zero.");
        }

        decimal depRate =
            Math.Round(100m / r.UsefulLifeYears, 4, MidpointRounding.AwayFromZero);

        var now = DateTime.UtcNow;

        var entity = new FixedAsset
        {
            BranchId = r.BranchId,
            Code = r.Code.Trim(),
            Name = r.Name.Trim(),
            PurchaseDateUtc = r.PurchaseDateUtc,
            PurchasePrice = r.PurchasePrice,
            UsefulLifeYears = r.UsefulLifeYears,
            DepreciationRatePercent = depRate,
            CreatedAtUtc = now,
            UpdatedAtUtc = null,
            IsDeleted = false,
            DeletedAtUtc = null
        };

        _db.FixedAssets.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new FixedAssetDetailDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.PurchaseDateUtc,
            entity.PurchasePrice,
            entity.UsefulLifeYears,
            entity.DepreciationRatePercent,
            entity.IsDeleted,
            Convert.ToBase64String(entity.RowVersion),
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc
        );
    }
}
