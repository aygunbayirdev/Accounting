using Accounting.Domain.Enums;
using MediatR;

namespace Accounting.Application.Payments.Commands.Create;

public record CreatePaymentCommand(
    int AccountId,
    int? ContactId,
    int? LinkedInvoiceId,
    DateTime DateUtc,
    PaymentDirection Direction,
    string Amount,
    string Currency,
    string? Description
) : IRequest<CreatePaymentResult>;

public record CreatePaymentResult(
    int Id,
    string Amount,
    string Currency
);
