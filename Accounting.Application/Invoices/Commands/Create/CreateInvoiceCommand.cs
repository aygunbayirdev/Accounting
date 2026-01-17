using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;

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
    string TotalNet,
    string TotalVat,
    string TotalGross,
    string RoundingPolicy
);
