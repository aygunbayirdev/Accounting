using Accounting.Application.Common.JsonConverters;
using Accounting.Domain.Enums;
using MediatR;
using System.Text.Json.Serialization;

namespace Accounting.Application.Payments.Commands.Create;

public record CreatePaymentCommand(
    int AccountId,
    int? ContactId,
    int? LinkedInvoiceId,
    DateTime DateUtc,
    PaymentDirection Direction,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Amount,

    string Currency,
    string? Description
) : IRequest<CreatePaymentResult>;

public record CreatePaymentResult(
    int Id,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Amount,

    string Currency
);
