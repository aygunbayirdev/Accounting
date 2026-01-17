using Accounting.Application.Common.Utils;
using FluentValidation;

namespace Accounting.Application.Items.Commands.Create;

public class CreateItemValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemValidator()
    {
        // RuleFor(x => x.BranchId).GreaterThan(0); // Removed
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(16);
        RuleFor(x => x.VatRate).InclusiveBetween(0, 100);
    }
}
