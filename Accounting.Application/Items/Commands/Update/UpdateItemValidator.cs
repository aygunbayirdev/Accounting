using Accounting.Domain.Enums;
using FluentValidation;

namespace Accounting.Application.Items.Commands.Update;

public class UpdateItemValidator : AbstractValidator<UpdateItemCommand>
{
    public UpdateItemValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.RowVersion).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(16);
        RuleFor(x => x.VatRate).InclusiveBetween(0, 100);

        RuleFor(x => x.DefaultWithholdingRate)
            .InclusiveBetween(0, 100)
            .When(x => x.DefaultWithholdingRate.HasValue);

        RuleFor(x => x.PurchasePrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.PurchasePrice.HasValue);

        RuleFor(x => x.SalesPrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.SalesPrice.HasValue);

        RuleFor(x => x.PurchaseAccountCode)
            .MaximumLength(16)
            .When(x => !string.IsNullOrEmpty(x.PurchaseAccountCode));

        RuleFor(x => x.SalesAccountCode)
            .MaximumLength(16)
            .When(x => !string.IsNullOrEmpty(x.SalesAccountCode));

        RuleFor(x => x.UsefulLifeYears)
            .GreaterThan(0)
            .LessThanOrEqualTo(50)
            .When(x => x.Type == (int)ItemType.FixedAsset && x.UsefulLifeYears.HasValue)
            .WithMessage("Demirbaşlar için faydalı ömür 1-50 yıl arası olmalıdır");
    }
}
