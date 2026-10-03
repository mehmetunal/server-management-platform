using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace ServerManager.Application.Deployments;

/// <summary>Dockerfile projelerinde <c>docker run -p</c> eşlemeleri: <c>8080:80</c>, <c>127.0.0.1:8080:80</c>, <c>5353:53/udp</c>.</summary>
public static partial class PortMappings
{
    public const int MaxCount = 20;

    public static bool TryParse(string? text, out IReadOnlyList<string> mappings, out string? error)
    {
        mappings = [];
        error = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var result = new List<string>();
        foreach (var token in text.Split([',', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = MappingPattern().Match(token);
            if (!match.Success
                || (match.Groups["ip"].Success && !IPAddress.TryParse(match.Groups["ip"].Value, out _))
                || !IsPort(match.Groups["host"].Value)
                || !IsPort(match.Groups["container"].Value))
            {
                error = $"\"{token}\" geçerli bir port eşlemesi değil (ör. 8080:80, 127.0.0.1:8080:80, 5353:53/udp).";
                return false;
            }

            result.Add(token);
        }

        if (result.Count > MaxCount)
        {
            error = $"En fazla {MaxCount} port eşlemesi tanımlanabilir.";
            return false;
        }

        mappings = result;
        return true;
    }

    private static bool IsPort(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is >= 1 and <= 65535;

    [GeneratedRegex(@"^(?:(?<ip>\d{1,3}(?:\.\d{1,3}){3}):)?(?<host>\d{1,5}):(?<container>\d{1,5})(?:/(?:tcp|udp))?$")]
    private static partial Regex MappingPattern();
}
