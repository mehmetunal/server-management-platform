using System.Net;
using System.Text.RegularExpressions;

namespace ServerManager.Application.Docker;

/// <summary>
/// Uzak sunucuda çalıştırılacak docker komutlarına giren kullanıcı girdileri burada doğrulanır.
/// Komutlar ayrıca tek tırnakla kaçışlanır; bu kurallar ikinci savunma hattıdır.
/// </summary>
public static partial class DockerNames
{
    public const int MaxLength = 255;

    public static readonly IReadOnlyList<string> NetworkDrivers = ["bridge", "overlay", "macvlan", "ipvlan"];

    public static bool IsValidContainerReference(string? value) =>
        IsValid(value) && ObjectNameRegex().IsMatch(value!);

    public static bool IsValidContainerName(string? value) =>
        IsValid(value) && value!.Length >= 2 && ObjectNameRegex().IsMatch(value);

    public static bool IsValidVolumeName(string? value) =>
        IsValid(value) && value!.Length >= 2 && ObjectNameRegex().IsMatch(value);

    public static bool IsValidVolumeReference(string? value) =>
        IsValid(value) && ObjectNameRegex().IsMatch(value!);

    public static bool IsValidNetworkName(string? value) =>
        IsValid(value) && value!.Length >= 2 && ObjectNameRegex().IsMatch(value);

    public static bool IsValidNetworkReference(string? value) =>
        IsValid(value) && ObjectNameRegex().IsMatch(value!);

    public static bool IsValidImageReference(string? value) =>
        IsValid(value) && ImageReferenceRegex().IsMatch(value!) && !value!.Contains("..", StringComparison.Ordinal);

    public static bool IsValidNetworkDriver(string? value) =>
        value is not null && NetworkDrivers.Contains(value, StringComparer.Ordinal);

    public static bool IsValidSubnet(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split('/');
        return parts.Length == 2
               && IPAddress.TryParse(parts[0], out var address)
               && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
               && int.TryParse(parts[1], out var prefix)
               && prefix is >= 8 and <= 30;
    }

    public static bool IsValidIpv4(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && IPAddress.TryParse(value, out var address)
        && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
        && value.Count(c => c == '.') == 3;

    /// <summary>docker logs --since için RFC3339 (nanosaniyeye kadar) veya Unix zaman damgası.</summary>
    public static bool IsValidLogSince(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= 40 && LogSinceRegex().IsMatch(value);

    private static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= MaxLength;

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,9})?(Z|[+-]\d{2}:\d{2})|\d{1,12}(\.\d{1,9})?)$")]
    private static partial Regex LogSinceRegex();

    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9_.-]*$")]
    private static partial Regex ObjectNameRegex();

    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9._/:@-]*$")]
    private static partial Regex ImageReferenceRegex();
}
