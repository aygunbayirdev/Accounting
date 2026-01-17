using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using Accounting.Application.Common.Extensions;
using Accounting.Application.Common.Interfaces;
using Accounting.Application.Contacts.Queries.Dto;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Contacts.Queries.GetById;

public class GetContactByIdHandler : IRequestHandler<GetContactByIdQuery, ContactDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    
    public GetContactByIdHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<ContactDetailDto> Handle(GetContactByIdQuery q, CancellationToken ct)
    {
        var c = await _db.Contacts
            .AsNoTracking()
            .Include(x => x.CompanyDetails)
            .Include(x => x.PersonDetails)
            .ApplyBranchFilter(_currentUserService)
            .FirstOrDefaultAsync(x => x.Id == q.Id, ct);

        if (c is null)
            throw new NotFoundException("Contact", q.Id);

        return new ContactDetailDto(
            c.Id, 
            c.BranchId, 
            c.Code, 
            c.Name, 
            c.IsCustomer,
            c.IsVendor,
            c.IsEmployee,
            c.IsRetail,
            c.Email,
            c.Phone,
            c.Iban,
            c.CompanyDetails != null ? new CompanyDetailsDto(c.CompanyDetails.TaxNumber, c.CompanyDetails.TaxOffice, c.CompanyDetails.MersisNo, c.CompanyDetails.TicaretSicilNo) : null,
            c.PersonDetails != null ? new PersonDetailsDto(c.PersonDetails.Tckn, c.PersonDetails.FirstName, c.PersonDetails.LastName, c.PersonDetails.Title, c.PersonDetails.Department) : null,
            Convert.ToBase64String(c.RowVersion),
            c.CreatedAtUtc, 
            c.UpdatedAtUtc
            );
    }
}
