using Accounting.Domain.Enums;
using FluentValidation;

namespace Accounting.Application.Contacts.Commands.Create;

public class CreateContactValidator : AbstractValidator<CreateContactCommand>
{
    public CreateContactValidator()
    {
        RuleFor(x => x.BranchId).GreaterThan(0);
        
        // Common
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.CompanyDetails != null);

        // Company Validation (If params provided)
        RuleFor(x => x.CompanyDetails!.TaxNumber).NotEmpty().Length(10).When(x => x.CompanyDetails != null);
        RuleFor(x => x.CompanyDetails!.TaxOffice).NotEmpty().MaximumLength(100).When(x => x.CompanyDetails != null);
        RuleFor(x => x.CompanyDetails!.MersisNo).MaximumLength(20).When(x => x.CompanyDetails != null);
        RuleFor(x => x.CompanyDetails!.TicaretSicilNo).MaximumLength(20).When(x => x.CompanyDetails != null);

        // Person Validation (If params provided)
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
    }
}
