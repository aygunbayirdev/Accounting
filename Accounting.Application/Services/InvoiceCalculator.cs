using Accounting.Application.Common.Utils;
using Accounting.Domain.Entities;

namespace Accounting.Application.Services;

public static class InvoiceCalculator
{
    public static (decimal totalNet, decimal totalVat, decimal totalGross) Recalculate(Invoice invoice)
    {
        decimal tNet = 0, tVat = 0;

        foreach (var l in invoice.Lines)
        {
            var net = DecimalExtensions.RoundAmount(l.Qty * l.UnitPrice);
            var vat = DecimalExtensions.RoundAmount(net * l.VatRate / 100m);
            var gross = net + vat;

            l.Net = net; l.Vat = vat; l.Gross = gross;

            tNet += net; tVat += vat;
        }

        invoice.TotalNet = DecimalExtensions.RoundAmount(tNet);
        invoice.TotalVat = DecimalExtensions.RoundAmount(tVat);
        invoice.TotalGross = invoice.TotalNet + invoice.TotalVat;

        return (invoice.TotalNet, invoice.TotalVat, invoice.TotalGross);
    }
}
