using MediatR;

namespace Accounting.Application.Orders.Commands.Approve;

public record ApproveOrderCommand(int Id, byte[] RowVersion) : IRequest<bool>;
