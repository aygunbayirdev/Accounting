namespace Accounting.Domain.Enums;

public enum ItemType
{
    Inventory = 1, // Stoklu Ürün (Fiziksel)
    Service = 2,    // Hizmet (Stok takibi yapılmaz)
    Expense = 3,
    FixedAsset = 4
}
