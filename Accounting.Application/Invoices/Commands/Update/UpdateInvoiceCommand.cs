using Accounting.Application.Invoices.Queries.Dto;
using MediatR;

public sealed record UpdateInvoiceCommand(
    int Id,
    string RowVersionBase64,
    DateTime DateUtc,
    string Currency,
    int ContactId,
    string Type,
    string? WaybillNumber,
    string? WaybillDateUtc,
    string? PaymentDueDateUtc,
    IReadOnlyList<UpdateInvoiceLineDto> Lines
) : IRequest<InvoiceDto>;

public sealed record UpdateInvoiceLineDto(
    int Id,          // 0 = new
    int? ItemId,
    int? ExpenseDefinitionId,
    string Qty,
    string UnitPrice,
    int VatRate,
    string? DiscountRate,
    int? WithholdingRate
);
