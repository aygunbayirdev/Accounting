using Accounting.Application.Common.Abstractions;
using Accounting.Application.Contacts.Queries.Dto;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;

namespace Accounting.Application.Contacts.Commands.Update;

public record UpdateContactCommand(
    int Id,

    // Flags
    bool IsCustomer,
    bool IsVendor,
    bool IsEmployee,
    bool IsRetail,
    // Common
    string Name,
    string? Email,
    string? Phone,
    string? Iban,
    // Address
    string? Address,
    string? City,
    string? District,
    // Details
    CompanyDetailsDto? CompanyDetails,
    PersonDetailsDto? PersonDetails,
    string RowVersion // base64
    ) : IRequest<ContactDto>;
