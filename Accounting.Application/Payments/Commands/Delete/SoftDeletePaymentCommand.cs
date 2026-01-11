namespace Accounting.Application.Payments.Commands.Delete;

using MediatR;

public record SoftDeletePaymentCommand(
    int Id,
    string RowVersion
) : IRequest;
