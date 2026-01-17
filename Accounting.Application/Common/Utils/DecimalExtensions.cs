using System.Globalization;

namespace Accounting.Application.Common.Utils;

public static class DecimalExtensions
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // =========================================================================
    // PUBLIC DOMAIN API
    // =========================================================================

    #region Amount (2 Hane - F2)
    // Finansal Tutarlar: Fatura Toplamı, Satır Tutarı, Kasa Bakiyesi vb.

    public static decimal RoundAmount(this decimal value) => RoundInternal(value, 2);
    public static string ToAmountString(this decimal value) => ToStringInternal(value, 2);
    public static bool TryParseAmount(this string? input, out decimal value) => TryParseInternal(input, 2, out value);

    #endregion

    #region Quantity (3 Hane - F3)
    // Fiziksel Miktarlar: Stok Adedi, Kilo, Litre, Metre vb.

    public static decimal RoundQuantity(this decimal value) => RoundInternal(value, 3);
    public static string ToQuantityString(this decimal value) => ToStringInternal(value, 3);
    public static bool TryParseQuantity(this string? input, out decimal value) => TryParseInternal(input, 3, out value);

    #endregion

    #region Currency & Unit Price (4 Hane - F4)
    // Hassas Değerler: Döviz Kuru, Vergi Oranı, Birim Fiyat vb.

    // Currency (Döviz Kuru)
    public static decimal RoundCurrency(this decimal value) => RoundInternal(value, 4);
    public static string ToCurrencyString(this decimal value) => ToStringInternal(value, 4);
    public static bool TryParseCurrency(this string? input, out decimal value) => TryParseInternal(input, 4, out value);

    // Unit Price (Birim Fiyat)
    public static decimal RoundUnitPrice(this decimal value) => RoundInternal(value, 4);
    public static string ToUnitPriceString(this decimal value) => ToStringInternal(value, 4);
    public static bool TryParseUnitPrice(this string? input, out decimal value) => TryParseInternal(input, 4, out value);

    #region Percent (2 Hane - F2)
    // Amortisman oranı, İskonto oranı (Genelde 2 hane yeterlidir: %18.00, %33.33)

    public static decimal RoundPercent(this decimal value) => RoundInternal(value, 2);
    public static string ToPercentString(this decimal value) => ToStringInternal(value, 2);
    public static bool TryParsePercent(this string? input, out decimal value) => TryParseInternal(input, 2, out value);

    #endregion

    #endregion

    // =========================================================================
    // INTERNAL HELPERS (JsonConverter Erişimi İçin)
    // =========================================================================

    internal static string ToStringByPrecision(this decimal value, int precision)
        => ToStringInternal(value, precision);

    internal static bool TryParseByPrecision(this string? input, int precision, out decimal value)
        => TryParseInternal(input, precision, out value);

    internal static decimal RoundByPrecision(this decimal value, int precision)
        => RoundInternal(value, precision);

    // =========================================================================
    // PRIVATE IMPLEMENTATION
    // =========================================================================

    private static decimal RoundInternal(decimal value, int decimals)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }

    private static string ToStringInternal(decimal value, int decimals)
    {
        return RoundInternal(value, decimals).ToString("F" + decimals, Inv);
    }

    private static bool TryParseInternal(string? input, int decimals, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(input)) return false;

        if (!decimal.TryParse(input.Trim(), NumberStyles.Number, Inv, out var parsed))
            return false;

        value = RoundInternal(parsed, decimals);
        return true;
    }
}