using MediatR;

namespace Accounting.Application.ExpenseLists.Commands.PostToBill;

public record PostExpenseListToBillCommand(
    int ExpenseListId,
    int SupplierId,
    int ItemId,
    string Currency,
    bool CreatePayment,
    int? PaymentAccountId = null,
    DateTime? PaymentDateUtc = null,
    DateTime? DateUtc = null
) : IRequest<PostExpenseListToBillResult>;

public record PostExpenseListToBillResult(
    int CreatedInvoiceId,
    int PostedExpenseCount
);