using MediatR;

namespace Accounting.Application.Cheques.Commands.Delete;

public record SoftDeleteChequeCommand(
    int Id,
    string RowVersionBase64
) : IRequest;
