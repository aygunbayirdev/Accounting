using Accounting.Application.Common.Utils;
using Accounting.Application.Common.Validation;
using FluentValidation;

namespace Accounting.Application.Items.Commands.Update;

public class UpdateItemValidator : AbstractValidator<UpdateItemCommand>
{
    public UpdateItemValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(16);
        RuleFor(x => x.VatRate).InclusiveBetween(0, 100);
        RuleFor(x => x.RowVersion).MustBeValidRowVersion();
    }
}
