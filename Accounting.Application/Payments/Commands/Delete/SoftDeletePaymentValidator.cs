using Accounting.Application.Common.Validation;
using FluentValidation;
using Accounting.Application.Payments.Commands.Delete;

namespace Accounting.Application.Payments.Commands.Delete;

public class SoftDeletePaymentValidator : AbstractValidator<SoftDeletePaymentCommand>
{
    public SoftDeletePaymentValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.RowVersion).MustBeValidRowVersion();
    }
}
