using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.CompanySettings.Dto;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.CompanySettings.Commands.Update;

public class UpdateCompanySettingsHandler : IRequestHandler<UpdateCompanySettingsCommand, CompanySettingsDetailDto>
{
    private readonly IAppDbContext _context;

    public UpdateCompanySettingsHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<CompanySettingsDetailDto> Handle(UpdateCompanySettingsCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.CompanySettings
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity == null)
        {
            // Normally singleton, so if not found with that ID, it's an error or we could try to find ANY.
            // But let's stick to explicit ID.
            throw new NotFoundException($"CompanySettings with ID {request.Id} not found.");
        }

        // Concurrency Check
        if (request.RowVersionBase64 != null)
        {
            var rv = Convert.FromBase64String(request.RowVersionBase64);
            if (!rv.SequenceEqual(entity.RowVersion))
            {
                throw new ConcurrencyConflictException("Firma ayarları başka bir kullanıcı tarafından değiştirildi. Lütfen sayfayı yenileyip tekrar deneyin.");
            }
        }

        // Update Fields
        entity.Title = request.Title.Trim();
        entity.TaxNumber = request.TaxNumber?.Trim();
        entity.TaxOffice = request.TaxOffice?.Trim();
        entity.Address = request.Address?.Trim();
        entity.Phone = request.Phone?.Trim();
        entity.Email = request.Email?.Trim();
        entity.Website = request.Website?.Trim();
        entity.TradeRegisterNo = request.TradeRegisterNo?.Trim();
        entity.MersisNo = request.MersisNo?.Trim();
        entity.LogoUrl = request.LogoUrl?.Trim();
        
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new CompanySettingsDetailDto(
            entity.Id,
            entity.Title,
            entity.TaxNumber,
            entity.TaxOffice,
            entity.Address,
            entity.Phone,
            entity.Email,
            entity.Website,
            entity.TradeRegisterNo,
            entity.MersisNo,
            entity.LogoUrl,
            Convert.ToBase64String(entity.RowVersion)
        );
    }
}
