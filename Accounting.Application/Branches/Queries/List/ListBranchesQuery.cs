using Accounting.Application.Branches.Queries.Dto;
using MediatR;

namespace Accounting.Application.Branches.Queries.List;

public sealed record ListBranchesQuery
    : IRequest<IReadOnlyList<BranchListItemDto>>;