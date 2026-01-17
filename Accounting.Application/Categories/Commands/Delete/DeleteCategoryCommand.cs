using MediatR;

namespace Accounting.Application.Categories.Commands.Delete;

public record DeleteCategoryCommand(int Id, string RowVersion) : IRequest<bool>;
