namespace ServerManager.Web.Models;

public sealed class PagerModel
{
    public int Page { get; init; }

    public int TotalPages { get; init; }

    public int TotalCount { get; init; }

    public string Action { get; init; } = "Index";

    public IDictionary<string, string?> RouteValues { get; init; } = new Dictionary<string, string?>();

    public IDictionary<string, string> RouteValuesFor(int page)
    {
        var values = RouteValues
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value!);
        values["page"] = page.ToString();
        return values;
    }
}
