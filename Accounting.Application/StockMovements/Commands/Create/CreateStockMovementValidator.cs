using Accounting.Application.Common.Utils;
using Accounting.Application.Common.Validation;
using FluentValidation;

namespace Accounting.Application.StockMovements.Commands.Create;

public class CreateStockMovementValidator : AbstractValidator<CreateStockMovementCommand>
{
    public CreateStockMovementValidator()
    {
        // RuleFor(x => x.BranchId).GreaterThan(0); // Removed
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.ItemId).GreaterThan(0);

        RuleFor(x => x.Note).MaximumLength(500);
    }
}
