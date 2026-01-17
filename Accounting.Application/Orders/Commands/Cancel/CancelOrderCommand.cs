using MediatR;

namespace Accounting.Application.Orders.Commands.Cancel;

public record CancelOrderCommand(int Id, string RowVersion) : IRequest<bool>;
