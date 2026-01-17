using Accounting.Application.Common.JsonConverters;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using System.Text.Json.Serialization;

namespace Accounting.Application.Invoices.Commands.Create;

public record CreateInvoiceCommand(
    int ContactId,
    DateTime DateUtc,
    string Currency,
    List<CreateInvoiceLineDto> Lines,
    InvoiceType Type,
    string? WaybillNumber,
    DateTime? WaybillDateUtc,
    DateTime? PaymentDueDateUtc
) : IRequest<CreateInvoiceResult>;

public record CreateInvoiceResult(
    int Id,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalNet,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalVat,

    [property: JsonConverter(typeof(AmountJsonConverter))]
    decimal TotalGross,

    string RoundingPolicy
);
