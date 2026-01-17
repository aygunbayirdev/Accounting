using Accounting.Application.Items.Queries.Dto;
using MediatR;

namespace Accounting.Application.Items.Commands.Create;

public record CreateItemCommand(
    int? CategoryId,
    string Code,
    string Name,
    int Type,                     // ItemType: 1=Inventory, 2=Service
    string Unit,
    int VatRate,                  // 0..100
    int? DefaultWithholdingRate,  // Varsayılan tevkifat oranı (%)
    string? PurchasePrice,        // string money
    string? SalesPrice            // string money
) : IRequest<ItemDetailDto>;
