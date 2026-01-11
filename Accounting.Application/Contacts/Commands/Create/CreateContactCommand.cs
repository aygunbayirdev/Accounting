using Accounting.Application.Common.Abstractions;
using Accounting.Application.Contacts.Queries.Dto;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;

namespace Accounting.Application.Contacts.Commands.Create;

public record CreateContactCommand(
    int BranchId,

    // Flags
    bool IsCustomer,
    bool IsVendor,
    bool IsEmployee,
    bool IsRetail,
    // Common
    string Name, // Unvan for Company, ignored if Person (derived)
    string? Email,
    string? Phone,
    string? Iban,
    // Address
    string? Address,
    string? City,
    string? District,
    // Details
    CompanyDetailsDto? CompanyDetails,
    PersonDetailsDto? PersonDetails
    ) : IRequest<ContactDto>;
