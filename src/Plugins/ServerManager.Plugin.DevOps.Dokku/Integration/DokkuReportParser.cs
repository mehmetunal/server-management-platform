using ServerManager.Plugin.DevOps.Dokku.DTOs;

namespace ServerManager.Plugin.DevOps.Dokku.Integration;

internal static class DokkuReportParser
{
    public static DokkuReport? Parse(string stdout)
    {
        var installed = false;
        var sawState = false;
        string? version = null;
        var apps = new List<DokkuAppDto>();

        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (line == "SM_STATE=missing")
            {
                sawState = true;
                installed = false;
                continue;
            }

            if (line == "SM_STATE=installed")
            {
                sawState = true;
                installed = true;
                continue;
            }

            if (line.StartsWith("SM_VERSION=", StringComparison.Ordinal))
            {
                version = EmptyToNull(line["SM_VERSION=".Length..]);
                continue;
            }

            if (!line.StartsWith("SM_APP=", StringComparison.Ordinal))
                continue;

            var parts = line["SM_APP=".Length..].Split('|', 4);
            if (parts.Length < 4 || !DokkuCommands.IsAppName(parts[0]))
                continue;

            apps.Add(new DokkuAppDto
            {
                Name = parts[0],
                Deployed = ParseBool(parts[1]),
                Running = ParseBool(parts[2]),
                Domains = EmptyToNull(parts[3])
            });
        }

        return sawState ? new DokkuReport(installed, version, apps) : null;
    }

    private static bool? ParseBool(string value) => value.Trim().ToLowerInvariant() switch
    {
        "true" => true,
        "false" => false,
        _ => null
    };

    private static string? EmptyToNull(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
