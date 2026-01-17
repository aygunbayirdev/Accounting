using Accounting.Application.Branches.Queries.Dto;
using MediatR;

namespace Accounting.Application.Branches.Queries.GetById;

public record GetBranchByIdQuery(int Id) : IRequest<BranchDetailDto>;