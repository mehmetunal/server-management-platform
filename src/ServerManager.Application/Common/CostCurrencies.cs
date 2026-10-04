namespace ServerManager.Application.Common;

public static class CostCurrencies
{
    public const string Default = "USD";

    public static readonly IReadOnlyList<string> All = ["USD", "EUR", "TRY", "GBP"];

    public static bool IsValid(string? currency) => currency is not null && All.Contains(currency);
}
