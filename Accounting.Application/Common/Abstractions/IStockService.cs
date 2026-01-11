namespace Accounting.Application.Common.Abstractions;

public interface IStockService
{
    Task<List<ItemStockDto>> GetStockStatusAsync(List<int> itemIds, CancellationToken ct);
    Task<ItemStockDto> GetItemStockAsync(int itemId, CancellationToken ct);
    Task ValidateStockAvailabilityAsync(int itemId, decimal quantityRequired, CancellationToken ct);
    Task ValidateBatchStockAvailabilityAsync(Dictionary<int, decimal> stockRequirements, CancellationToken ct);
}

public record ItemStockDto(
    int ItemId,
    decimal QuantityIn,       // Giren (Alış Faturaları)
    decimal QuantityOut,      // Çıkan (Satış Faturaları)
    decimal QuantityReserved, // Rezerve (Onaylı Satış Siparişleri)
    decimal QuantityAvailable // Kullanılabilir (Giren - Çıkan - Rezerve)
);
