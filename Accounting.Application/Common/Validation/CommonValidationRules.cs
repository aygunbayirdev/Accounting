using Accounting.Application.Common.Utils;
using FluentValidation;

namespace Accounting.Application.Common.Validation;

/// <summary>
/// Tüm validator'larda kullanılabilecek ortak validation kuralları
/// </summary>
public static class CommonValidationRules
{
    // ========== Allowed Values ==========

    /// <summary>
    /// Desteklenen para birimleri (ISO-4217)
    /// </summary>
    public static readonly string[] AllowedCurrencies = { "TRY", "USD", "EUR", "GBP" };

    // ========== Helper Methods for Handlers ==========

    /// <summary>
    /// Currency kodunu normalize et ve validate et.
    /// Handler'larda kullanım için.
    /// </summary>
    /// <param name="currency">Input currency (nullable)</param>
    /// <param name="defaultCurrency">Default değer (varsayılan: TRY)</param>
    /// <returns>Normalized uppercase currency code</returns>
    /// <exception cref="FluentValidation.ValidationException">Geçersiz currency</exception>
    public static string NormalizeAndValidateCurrency(string? currency, string defaultCurrency = "TRY")
    {
        var normalized = (currency ?? defaultCurrency).ToUpperInvariant();

        if (!AllowedCurrencies.Contains(normalized))
            throw new ValidationException($"Currency '{currency}' is not supported. Allowed: {string.Join(", ", AllowedCurrencies)}");

        return normalized;
    }

    /// <summary>
    /// Currency kodunun geçerli olup olmadığını kontrol et (exception fırlatmadan)
    /// </summary>
    public static bool IsValidCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency)) return false;
        return AllowedCurrencies.Contains(currency.ToUpperInvariant());
    }

    /// <summary>
    /// Currency code validation (ISO-4217 whitelist)
    /// </summary>
    public static IRuleBuilderOptions<T, string?> MustBeValidCurrency<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .Length(3)
            .Must(BeValidCurrency)
            .WithMessage("'{PropertyName}' must be a valid currency code (TRY, USD, EUR, GBP).");
    }

    /// <summary>
    /// RowVersion Base64 validation
    /// </summary>
    public static IRuleBuilderOptions<T, string?> MustBeValidRowVersion<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .Must(BeValidBase64)
            .WithMessage("'{PropertyName}' must be a valid Base64 string.");
    }

    // ========== Private Helper Methods ==========

    private static bool BeValidCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency)) return false;
        return AllowedCurrencies.Contains(currency.ToUpperInvariant());
    }

    private static bool BeValidBase64(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return false;

        try
        {
            Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}