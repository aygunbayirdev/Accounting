using Accounting.Application.Categories.Queries;
using Accounting.Application.Common.Abstractions;
using Accounting.Domain.Entities;
using MediatR;

namespace Accounting.Application.Categories.Commands.Create;

public class CreateCategoryHandler(IAppDbContext db) : IRequestHandler<CreateCategoryCommand, CategoryDetailDto>
{
    public async Task<CategoryDetailDto> Handle(CreateCategoryCommand r, CancellationToken ct)
    {
        var category = new Category
        {
            Name = r.Name.Trim(),
            Description = r.Description?.Trim(),
            Color = r.Color?.Trim(),
            RowVersion = []
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);

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
