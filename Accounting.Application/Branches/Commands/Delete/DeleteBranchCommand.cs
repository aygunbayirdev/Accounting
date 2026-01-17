using MediatR;

namespace Accounting.Application.Branches.Commands.Delete;

public record DeleteBranchCommand(int Id, string RowVersionBase64) : IRequest;
