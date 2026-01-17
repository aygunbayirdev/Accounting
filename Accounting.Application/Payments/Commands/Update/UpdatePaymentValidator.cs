using Accounting.Application.Common.Validation;
using FluentValidation;
using Accounting.Application.Payments.Commands.Update;

namespace Accounting.Application.Payments.Commands.Update;

public class UpdatePaymentValidator : AbstractValidator<UpdatePaymentCommand>
{
    public UpdatePaymentValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.AccountId).GreaterThan(0);
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.DateUtc).NotEmpty().WithMessage("DateUtc gereklidir.");
        RuleFor(x => x.Currency).MustBeValidCurrency(); // Extension
        RuleFor(x => x.RowVersion).MustBeValidRowVersion(); // Extension

        When(x => x.ContactId.HasValue, () =>
        {
            RuleFor(x => x.ContactId!.Value).GreaterThan(0);
        });
        When(x => x.LinkedInvoiceId.HasValue, () =>
        {
            RuleFor(x => x.LinkedInvoiceId!.Value).GreaterThan(0);
        });
    }
}
