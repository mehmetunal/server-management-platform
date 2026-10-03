using System.Text.Json;

namespace ServerManager.Application.Notifications;

public static class NotificationSettingsSerializer
{
    public static string Serialize(IReadOnlyDictionary<string, string> settings) =>
        JsonSerializer.Serialize(settings);

    public static Dictionary<string, string> Deserialize(string json)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        return values is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(values, StringComparer.Ordinal);
    }
}
