using Accounting.Application.Invoices.Queries.Dto;
using MediatR;

namespace Accounting.Application.Invoices.Queries.GetById;

public record GetInvoiceByIdQuery(int Id) : IRequest<InvoiceDetailDto>;

