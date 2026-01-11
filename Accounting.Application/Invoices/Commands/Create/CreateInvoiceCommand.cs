using Accounting.Domain.Entities;
using MediatR;

namespace Accounting.Application.Invoices.Commands.Create;

public record CreateInvoiceCommand(
    int ContactId,
    string DateUtc,
    string Currency,
    List<CreateInvoiceLineDto> Lines,
    string Type, // "Sales", "Purchase" vb.
    string? WaybillNumber,
    string? WaybillDateUtc,
    string? PaymentDueDateUtc
) : IRequest<CreateInvoiceResult>;

public record CreateInvoiceResult(
    int Id,
    string TotalNet,
    string TotalVat,
    string TotalGross,
    string RoundingPolicy
);
