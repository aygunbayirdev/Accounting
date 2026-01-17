using System.Text.Json;
using System.Text.Json.Serialization;
using Accounting.Application.Common.Utils;

namespace Accounting.Application.Common.JsonConverters;

// --- BASE CLASS ---
public abstract class DecimalToStringConverterBase : JsonConverter<decimal>
{
    private readonly int _precision;

    protected DecimalToStringConverterBase(int precision)
    {
        _precision = precision;
    }

    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // 1. String gelirse ("150.50") -> Parse et
        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (stringValue.TryParseByPrecision(_precision, out var value))
            {
                return value;
            }
        }

        // 2. Number gelirse (150.5) -> Yuvarla
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal().RoundByPrecision(_precision);
        }

        throw new JsonException($"Invalid format. Expected string or number with precision {_precision}.");
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        // Backend -> Frontend (String formatında)
        writer.WriteStringValue(value.ToStringByPrecision(_precision));
    }
}

// --- CONCRETE CONVERTERS ---

/// <summary>
/// 2 Hane Hassasiyet (Tutar) -> "100.50"
/// Fatura toplamı, satır tutarı vb.
/// </summary>
public class AmountJsonConverter : DecimalToStringConverterBase
{
    public AmountJsonConverter() : base(2) { }
}

/// <summary>
/// 3 Hane Hassasiyet (Miktar) -> "1.500"
/// Stok adedi, kilo, metre vb.
/// </summary>
public class QuantityJsonConverter : DecimalToStringConverterBase
{
    public QuantityJsonConverter() : base(3) { }
}

/// <summary>
/// 4 Hane Hassasiyet (Döviz Kuru) -> "34.1234"
/// </summary>
public class CurrencyJsonConverter : DecimalToStringConverterBase
{
    public CurrencyJsonConverter() : base(4) { }
}

/// <summary>
/// 4 Hane Hassasiyet (Birim Fiyat) -> "10.5045"
/// Maliyet hesaplamaları için hassas fiyat.
/// </summary>
public class UnitPriceJsonConverter : DecimalToStringConverterBase
{
    public UnitPriceJsonConverter() : base(4) { }
}

/// <summary>
/// 2 Hane Hassasiyet (Yüzde Oranları) -> "20.00", "33.33"
/// Amortisman, Vergi, İskonto oranları için.
/// </summary>
public class PercentJsonConverter : DecimalToStringConverterBase
{
    // Yüzdeler genelde 2 hane gösterilir. 
    // Eğer 4 hane lazımsa (Örn: Faiz) base(4) yapabilirsin.
    public PercentJsonConverter() : base(2) { }
}