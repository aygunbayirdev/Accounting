using MediatR;

namespace Accounting.Application.Orders.Commands.CreateInvoice;
public record CreateInvoiceFromOrderCommand(int OrderId) : IRequest<int>;