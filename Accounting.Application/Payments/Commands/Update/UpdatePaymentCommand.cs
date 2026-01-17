namespace Accounting.Application.Payments.Commands.Update;

using Accounting.Application.Payments.Queries.Dto;
using Accounting.Domain.Enums;
using MediatR;

public record UpdatePaymentCommand(
    int Id,
    int AccountId,
    int? ContactId,
    int? LinkedInvoiceId,
    DateTime DateUtc,
    PaymentDirection Direction,
    string Amount,
    string Currency,
    string? Description,
    string RowVersion
) : IRequest<PaymentDetailDto>;
