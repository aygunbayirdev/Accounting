using Accounting.Application.Common.Validation;
using FluentValidation;

namespace Accounting.Application.ExpenseLists.Commands.Create;

public class CreateExpenseListValidator : AbstractValidator<CreateExpenseListCommand>
{
    public CreateExpenseListValidator()
    {
        RuleFor(x => x.BranchId).GreaterThan(0);

        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("At least one expense line is required.");

        RuleForEach(x => x.Lines).SetValidator(new CreateExpenseLineDtoValidator());
    }
}

public class CreateExpenseLineDtoValidator : AbstractValidator<CreateExpenseLineDto>
{
    public CreateExpenseLineDtoValidator()
    {
        RuleFor(x => x.DateUtc).NotEmpty().WithMessage("DateUtc gereklidir.");
        RuleFor(x => x.Currency).MustBeValidCurrency();

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Tutar 0'dan büyük olmalıdır.");

        RuleFor(x => x.VatRate).InclusiveBetween(0, 100);
    }
}
