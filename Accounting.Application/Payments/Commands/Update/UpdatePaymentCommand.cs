namespace Accounting.Application.Payments.Commands.Update;

using Accounting.Application.Common.JsonConverters;
using Accounting.Application.Payments.Queries.Dto;
using Accounting.Domain.Enums;
using MediatR;
using System.Text.Json.Serialization;

public record UpdatePaymentCommand(
    int Id,
    int AccountId,
    int? ContactId,
    int? LinkedInvoiceId,
    DateTime DateUtc,
    PaymentDirection Direction,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal Amount,

    string Currency,
    string? Description,
    string RowVersion
) : IRequest<PaymentDetailDto>;
