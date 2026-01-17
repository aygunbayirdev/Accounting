using Accounting.Application.CompanySettings.Dto;
using MediatR;

namespace Accounting.Application.CompanySettings.Commands.Update;

public record UpdateCompanySettingsCommand(
    int Id,
    string Title,
    string? TaxNumber,
    string? TaxOffice,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    string? TradeRegisterNo,
    string? MersisNo,
    string? LogoUrl,
    string? RowVersionBase64
) : IRequest<CompanySettingsDetailDto>;
