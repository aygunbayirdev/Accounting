using Accounting.Application.Common.Validation;
using FluentValidation;

namespace Accounting.Application.Contacts.Commands.Update;

public class UpdateContactValidator : AbstractValidator<UpdateContactCommand>
{
    public UpdateContactValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(320).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Iban).MaximumLength(34);
        RuleFor(x => x.Address).MaximumLength(500);

        // Company Validation (Nested)
        RuleFor(x => x.CompanyDetails!.TaxNumber).NotEmpty().Length(10).When(x => x.CompanyDetails != null);
        RuleFor(x => x.CompanyDetails!.TaxOffice).NotEmpty().MaximumLength(100).When(x => x.CompanyDetails != null);
        RuleFor(x => x.CompanyDetails!.MersisNo).MaximumLength(20).When(x => x.CompanyDetails != null);
        RuleFor(x => x.CompanyDetails!.TicaretSicilNo).MaximumLength(20).When(x => x.CompanyDetails != null);

        // Person Validation (Nested)
        RuleFor(x => x.PersonDetails!.Tckn).NotEmpty().Length(11).When(x => x.PersonDetails != null);
        RuleFor(x => x.PersonDetails!.FirstName).NotEmpty().MaximumLength(100).When(x => x.PersonDetails != null);
        RuleFor(x => x.PersonDetails!.LastName).NotEmpty().MaximumLength(100).When(x => x.PersonDetails != null);
        RuleFor(x => x.PersonDetails!.Title).MaximumLength(100).When(x => x.PersonDetails != null);
        RuleFor(x => x.PersonDetails!.Department).MaximumLength(100).When(x => x.PersonDetails != null);

        // Employee MUST have PersonDetails
        RuleFor(x => x.PersonDetails).NotNull().When(x => x.IsEmployee)
            .WithMessage("Personel (Employee) kaydı mutlaka Şahıs bilgilerini (Person Details) içermelidir.");
        
        // IsRetail=true can NOT be IsCustomer=true
        RuleFor(x => x.IsCustomer).Equal(false).When(x => x.IsRetail)
            .WithMessage("Perakende müşteri (Retail) aynı zamanda Kurumsal Müşteri (Customer) olamaz.");
            
        // At least ONE detail must be present (Company OR Person)
        RuleFor(x => x).Must(x => x.CompanyDetails != null || x.PersonDetails != null)
            .WithMessage("Cari kart Şahıs veya Şirket bilgilerinden en az birini içermelidir.");

        RuleFor(x => x.RowVersion).MustBeValidRowVersion();
    }
}
