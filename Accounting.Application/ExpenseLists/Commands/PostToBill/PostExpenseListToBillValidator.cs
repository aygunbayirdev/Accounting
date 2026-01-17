using Accounting.Application.Common.Validation;
using FluentValidation;

namespace Accounting.Application.ExpenseLists.Commands.PostToBill;

public class PostExpenseListToBillValidator : AbstractValidator<PostExpenseListToBillCommand>
{
    public PostExpenseListToBillValidator()
    {
        RuleFor(x => x.ExpenseListId).GreaterThan(0);
        RuleFor(x => x.SupplierId).GreaterThan(0);
        RuleFor(x => x.ItemId).GreaterThan(0);
        RuleFor(x => x.Currency).MustBeValidCurrency();

        // DateUtc ve PaymentDateUtc DateTime? oldu - .NET otomatik parse ediyor
        // Artık string validation gerekmez

        // ✅ CreatePayment=true ise PaymentAccountId zorunlu
        When(x => x.CreatePayment, () =>
        {
            RuleFor(x => x.PaymentAccountId)
                .NotNull()
                .WithMessage("PaymentAccountId is required when CreatePayment is true.");

            RuleFor(x => x.PaymentAccountId!.Value)
                .GreaterThan(0)
                .WithMessage("PaymentAccountId must be a valid account ID.");
        });
    }
}