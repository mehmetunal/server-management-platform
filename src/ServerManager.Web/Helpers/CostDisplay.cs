using System.Globalization;
using ServerManager.Application.Common;

namespace ServerManager.Web.Helpers;

public static class CostDisplay
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string Amount(decimal amount, string? currency = null) =>
        amount.ToString("N2", Turkish) + " " + (string.IsNullOrWhiteSpace(currency) ? CostCurrencies.Default : currency);

    public static string Optional(decimal? amount, string? currency) =>
        amount.HasValue ? Amount(amount.Value, currency) : "—";
}
