using Accounting.Application.Branches.Queries.Dto;
using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Branches.Queries.GetById;

public record GetBranchByIdQuery(int Id) : IRequest<BranchDetailDto>;

public class GetBranchByIdHandler : IRequestHandler<GetBranchByIdQuery, BranchDetailDto>
{
    private readonly IAppDbContext _context;

    public GetBranchByIdHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<BranchDetailDto> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        
        // Note: Global query filter handles IsDeleted check automatically

        if (entity == null)
            throw new NotFoundException("Branch", request.Id);

        return new BranchDetailDto(
            entity.Id,
            entity.Code,
            entity.Name,
            Convert.ToBase64String(entity.RowVersion),
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc
        );
    }
}
