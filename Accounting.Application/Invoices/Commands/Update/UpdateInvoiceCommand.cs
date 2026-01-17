using Accounting.Application.Invoices.Queries.Dto;
using Accounting.Domain.Enums;
using MediatR;

public sealed record UpdateInvoiceCommand(
    int Id,
    string RowVersionBase64,
    DateTime DateUtc,
    string Currency,
    int ContactId,
    InvoiceType Type,
    string? WaybillNumber,
    DateTime? WaybillDateUtc,
    DateTime? PaymentDueDateUtc,
    IReadOnlyList<UpdateInvoiceLineDto> Lines
) : IRequest<InvoiceDto>;

public sealed record UpdateInvoiceLineDto(
    int Id,
    int? ItemId,
    int? ExpenseDefinitionId,
    string Qty,
    string UnitPrice,
    int VatRate,
    string? DiscountRate,
    int? WithholdingRate
);
