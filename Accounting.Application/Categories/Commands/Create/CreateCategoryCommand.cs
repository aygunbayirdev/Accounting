using Accounting.Application.Categories.Queries;
using MediatR;

namespace Accounting.Application.Categories.Commands.Create;

public record CreateCategoryCommand(
    string Name,
    string? Description,
    string? Color
) : IRequest<CategoryDetailDto>;
