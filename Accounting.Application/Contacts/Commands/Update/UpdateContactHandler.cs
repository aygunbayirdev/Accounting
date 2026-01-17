using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions; // ConcurrencyConflictException
using Accounting.Application.Common.Extensions; // ApplyBranchFilter
using Accounting.Application.Common.Interfaces; // ICurrentUserService
using Accounting.Application.Contacts.Queries.Dto;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Contacts.Commands.Update;

public class UpdateContactHandler : IRequestHandler<UpdateContactCommand, ContactDetailDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public UpdateContactHandler(IAppDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<ContactDetailDto> Handle(UpdateContactCommand req, CancellationToken ct)
    {
        // 1) Fetch (TRACKING) + Includes
        var c = await _db.Contacts
            .Include(x => x.CompanyDetails)
            .Include(x => x.PersonDetails)
            .ApplyBranchFilter(_currentUserService)
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct);

        if (c is null) throw new NotFoundException("Contact", req.Id);

        // 3) Concurrency
        byte[] rv;
        try { rv = Convert.FromBase64String(req.RowVersion); }
        catch { throw new ConcurrencyConflictException("RowVersion geçersiz."); }
        _db.Entry(c).Property(nameof(Contact.RowVersion)).OriginalValue = rv;

        // 4) Normalize / map
        
        // Derive Name
        string displayName = req.Name;
        if (req.PersonDetails != null)
        {
            displayName = $"{req.PersonDetails.FirstName} {req.PersonDetails.LastName}".Trim();
        }
        c.Name = displayName;
        
        c.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        c.Phone = req.Phone;
        c.Iban = req.Iban;
        c.Address = req.Address;
        c.City = req.City;
        c.District = req.District;
        
        // Flags
        c.IsCustomer = req.IsCustomer;
        c.IsVendor = req.IsVendor;
        c.IsEmployee = req.IsEmployee;
        c.IsRetail = req.IsRetail;

        // Identity Logic: If details provided, update/create. If null, LEAVE AS IS or REMOVE?
        // Command usually sends FULL state. If PersonDetails is null in request, it implies "Remove Person Identity" OR "No Change"?
        // Typically PUT is full replace. We assume if Details is null, we remove it.
        
        // Update Company Details
        if (req.CompanyDetails != null)
        {
            if (c.CompanyDetails == null) c.CompanyDetails = new CompanyDetails();
            c.CompanyDetails.TaxNumber = req.CompanyDetails.TaxNumber;
            c.CompanyDetails.TaxOffice = req.CompanyDetails.TaxOffice;
            c.CompanyDetails.MersisNo = req.CompanyDetails.MersisNo;
            c.CompanyDetails.TicaretSicilNo = req.CompanyDetails.TicaretSicilNo;
        }
        else
        {
            // If request has null details, but DB has details -> Remove them?
            // YES, to switch identity or clean up.
            if (c.CompanyDetails != null)
            {
                _db.CompanyDetails.Remove(c.CompanyDetails);
                c.CompanyDetails = null;
            }
        }

        // Update Person Details
        if (req.PersonDetails != null)
        {
            if (c.PersonDetails == null) c.PersonDetails = new PersonDetails();
            c.PersonDetails.Tckn = req.PersonDetails.Tckn;
            c.PersonDetails.FirstName = req.PersonDetails.FirstName;
            c.PersonDetails.LastName = req.PersonDetails.LastName;
            c.PersonDetails.Title = req.PersonDetails.Title;
            c.PersonDetails.Department = req.PersonDetails.Department;
        }
        else
        {
             if (c.PersonDetails != null)
            {
                _db.PersonDetails.Remove(c.PersonDetails);
                c.PersonDetails = null;
            }
        }

        if (req.IsEmployee && c.PersonDetails == null)
             throw new Exception("Personel (Employee) kaydı mutlaka Şahıs bilgilerini içermelidir.");

        // 5) Audit
        c.UpdatedAtUtc = DateTime.UtcNow;

        // 6) Persist
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { throw new ConcurrencyConflictException("Cari başka biri tarafından güncellendi."); }

        // 7) Fresh read
        var fresh = await _db.Contacts
            .Include(x => x.CompanyDetails)
            .Include(x => x.PersonDetails)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct);
            
        if (fresh is null) throw new NotFoundException("Contact", req.Id);

        // 8) DTO
        return new ContactDetailDto(
            fresh.Id,
            fresh.BranchId,
            fresh.Code,
            fresh.Name,
            fresh.IsCustomer,
            fresh.IsVendor,
            fresh.IsEmployee,
            fresh.IsRetail,
            fresh.Email,
            fresh.Phone,
            fresh.Iban,
            fresh.CompanyDetails != null ? new CompanyDetailsDto(fresh.CompanyDetails.TaxNumber, fresh.CompanyDetails.TaxOffice, fresh.CompanyDetails.MersisNo, fresh.CompanyDetails.TicaretSicilNo) : null,
            fresh.PersonDetails != null ? new PersonDetailsDto(fresh.PersonDetails.Tckn, fresh.PersonDetails.FirstName, fresh.PersonDetails.LastName, fresh.PersonDetails.Title, fresh.PersonDetails.Department) : null,
            Convert.ToBase64String(fresh.RowVersion),
            fresh.CreatedAtUtc,
            fresh.UpdatedAtUtc
        );
    }
}
