using Accounting.Application.Branches.Queries.Dto;
using MediatR;

namespace Accounting.Application.Branches.Commands.Create;

public record CreateBranchCommand(string Code, string Name) : IRequest<BranchDetailDto>;
