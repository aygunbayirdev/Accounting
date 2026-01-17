using Accounting.Application.Common.Abstractions;
using Accounting.Application.CompanySettings.Dto;
using Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.CompanySettings.Queries.Get;

public record GetCompanySettingsQuery : IRequest<CompanySettingsDetailDto>;

public class GetCompanySettingsHandler : IRequestHandler<GetCompanySettingsQuery, CompanySettingsDetailDto>
{
    private readonly IAppDbContext _context;

    public GetCompanySettingsHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<CompanySettingsDetailDto> Handle(GetCompanySettingsQuery request, CancellationToken cancellationToken)
    {
        // Singleton: First or Default. If null, we might return empty or throw. 
        // But Seeder ensures it exists. If not, we can create one on the fly or return default.
        // Let's assume it exists or return default empty.
        
        var entity = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (entity == null) 
        {
             // Fallback if not seeded? Or throw?
             // Since it is a singleton, let's just return a default DTO if database is empty (should not happen in prod if seeded)
             return new CompanySettingsDetailDto(0, "Not Configured", null, null, null, null, null, null, null, null, null, null);
        }

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
            entity.RowVersion != null ? Convert.ToBase64String(entity.RowVersion) : null
        );
    }
}
