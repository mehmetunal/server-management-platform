using System.Text.Json;

namespace ServerManager.Application.Security;

public static class SecurityReportSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(SecurityReport report) => JsonSerializer.Serialize(report, Options);

    /// <summary>Okunamayan (eski/bozuk) rapor için boş döner.</summary>
    public static SecurityReport? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SecurityReport>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
