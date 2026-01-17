using FluentValidation;

namespace Accounting.Application.StockMovements.Commands.Create;

public class CreateStockMovementValidator : AbstractValidator<CreateStockMovementCommand>
{
    public CreateStockMovementValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.ItemId).GreaterThan(0);

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Miktar 0'dan büyük olmalıdır.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Geçersiz stok hareket tipi.");

        RuleFor(x => x.Note).MaximumLength(500);
    }
}
