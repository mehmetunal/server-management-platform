namespace ServerManager.Web.Helpers;

public static class CloudDisplay
{
    public static string StatusText(string status) => status.ToLowerInvariant() switch
    {
        "running" or "active" => "Çalışıyor",
        "off" => "Kapalı",
        "initializing" or "starting" or "new" => "Hazırlanıyor",
        "stopping" => "Durduruluyor",
        "rebuilding" or "migrating" => "Bakımda",
        "deleting" => "Siliniyor",
        "archive" => "Arşivde",
        _ => status
    };

    public static string StatusBadgeClass(string status) => status.ToLowerInvariant() switch
    {
        "running" or "active" => "badge-success",
        "off" or "archive" => "badge-neutral",
        "deleting" => "badge-danger",
        _ => "badge-info"
    };

    public static string Costs(IReadOnlyDictionary<string, decimal> costs) =>
        costs.Count == 0 ? "—" : string.Join(" + ", costs.OrderBy(c => c.Key).Select(c => CostDisplay.Amount(c.Value, c.Key)));
}
