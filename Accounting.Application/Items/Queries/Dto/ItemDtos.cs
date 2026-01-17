namespace Accounting.Application.Items.Queries.Dto;

public record ItemListItemDto(
    int Id,
    int? CategoryId,
    string? CategoryName,
    string Code,
    string Name,
    int Type,                       // ItemType enum (1=Inventory, 2=Service)
    string Unit,
    int VatRate,
    int DefaultWithholdingRate,     // Varsayılan tevkifat oranı
    string? PurchasePrice,          // money string 
    string? SalesPrice,             // money string
    DateTime CreatedAtUtc
);

public record ItemDetailDto(
    int Id,
    int? CategoryId,
    string? CategoryName,
    string Code,                    // Stok kodu
    string Name,
    int Type,                       // ItemType enum (1=Inventory, 2=Service)
    string Unit,
    int VatRate,
    int DefaultWithholdingRate,     // Varsayılan tevkifat oranı
    string? PurchasePrice,          // money string
    string? SalesPrice,             // money string
    string RowVersion,              // base64
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
