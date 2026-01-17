using MediatR;

namespace Accounting.Application.Orders.Commands.Delete;

public record DeleteOrderCommand(int Id, string RowVersion) : IRequest<bool>;
