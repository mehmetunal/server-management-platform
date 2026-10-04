using ServerManager.Application.DTOs.ServerGroups;

namespace ServerManager.Web.Helpers;

public static class ServerGroupDisplay
{
    private static readonly IReadOnlyDictionary<string, string> ColorNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["slate"] = "Gri",
        ["blue"] = "Mavi",
        ["green"] = "Yeşil",
        ["amber"] = "Turuncu",
        ["red"] = "Kırmızı",
        ["purple"] = "Mor",
        ["cyan"] = "Turkuaz",
        ["pink"] = "Pembe"
    };

    public static string BadgeClass(string? color) =>
        "group-badge group-badge-" + (ServerGroupColors.IsValid(color) ? color : ServerGroupColors.Default);

    public static string ColorName(string color) => ColorNames.GetValueOrDefault(color, color);
}
