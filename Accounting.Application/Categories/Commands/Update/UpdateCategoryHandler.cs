using Accounting.Application.Categories.Queries;
using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Application.Categories.Commands.Update;

public record UpdateCategoryCommand(
    int Id,
    string Name,
    string? Description,
    string? Color,
    string RowVersion
) : IRequest<CategoryDetailDto>;

public class UpdateCategoryHandler(IAppDbContext db) : IRequestHandler<UpdateCategoryCommand, CategoryDetailDto>
{
    public async Task<CategoryDetailDto> Handle(UpdateCategoryCommand r, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(x => x.Id == r.Id && !x.IsDeleted, ct);
        if (category is null)
        {
            throw new NotFoundException("Category", r.Id);
        }

        db.Entry(category).Property(nameof(category.RowVersion)).OriginalValue = Convert.FromBase64String(r.RowVersion);

        category.Name = r.Name.Trim();
        category.Description = r.Description?.Trim();
        category.Color = r.Color?.Trim();
        category.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("Kategori başka bir kullanıcı tarafından değiştirildi.");
        }

        return new CategoryDetailDto(
            category.Id,
            category.Name,
            category.Description,
            category.Color,
            Convert.ToBase64String(category.RowVersion),
            category.CreatedAtUtc,
            category.UpdatedAtUtc
        );
    }
}
