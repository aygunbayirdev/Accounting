using Accounting.Application.Items.Queries.Dto;
using MediatR;
using System.Data;

namespace Accounting.Application.Items.Commands.Update;

public record UpdateItemCommand(
    int Id,
    int? CategoryId,
    string Code,                  // Stok kodu
    string Name,
    int Type,                     // ItemType: 1=Inventory, 2=Service
    string Unit,
    int VatRate,
    int? DefaultWithholdingRate,  // Varsayılan tevkifat oranı (%)
    string? PurchasePrice,
    string? SalesPrice,
    string RowVersion             // base64
) : IRequest<ItemDetailDto>;
