using Accounting.Application.Categories.Queries;
using MediatR;

namespace Accounting.Application.Categories.Commands.Update;

public record UpdateCategoryCommand(
    int Id,
    string Name,
    string? Description,
    string? Color,
    string RowVersion
) : IRequest<CategoryDetailDto>;
