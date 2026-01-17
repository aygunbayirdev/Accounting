using Accounting.Application.Common.Validation;
using FluentValidation;

namespace Accounting.Application.ExpenseLists.Commands.Update;

public class UpdateExpenseListValidator : AbstractValidator<UpdateExpenseListCommand>
{
    public UpdateExpenseListValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .WithMessage("RowVersion is required for concurrency control.");

        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("At least one expense line is required.");

        RuleForEach(x => x.Lines).SetValidator(new UpdateExpenseLineDtoValidator());
    }
}

public class UpdateExpenseLineDtoValidator : AbstractValidator<UpdateExpenseLineDto>
{
    public UpdateExpenseLineDtoValidator()
    {
        // Id pozitif olmalı (eğer dolu ise)
        When(x => x.Id.HasValue, () =>
        {
            RuleFor(x => x.Id!.Value).GreaterThan(0);
        });

        RuleFor(x => x.DateUtc).NotEmpty().WithMessage("DateUtc gereklidir.");
        RuleFor(x => x.Currency).MustBeValidCurrency();
        RuleFor(x => x.VatRate).InclusiveBetween(0, 100);
    }
}