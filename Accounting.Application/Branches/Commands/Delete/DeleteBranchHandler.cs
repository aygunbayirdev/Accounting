using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Branches.Commands.Delete;


public class DeleteBranchHandler : IRequestHandler<DeleteBranchCommand>
{
    private readonly IAppDbContext _context;

    public DeleteBranchHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Branches
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity == null)
            throw new NotFoundException("Branch", request.Id);

        // Concurrency Check
        var rv = Convert.FromBase64String(request.RowVersionBase64);
        if (!rv.SequenceEqual(entity.RowVersion))
        {
            throw new ConcurrencyConflictException("Şube bilgisi başka bir kullanıcı tarafından değiştirildi.");
        }

        // Soft Delete
        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
