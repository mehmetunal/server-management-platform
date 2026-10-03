using System.Text.RegularExpressions;

namespace ServerManager.Plugin.DevOps.Dokploy.Core;

public static partial class DokployVersions
{
    public const int MaxLength = 64;

    /// <summary>Kurulum betiğinin kabul ettiği etiketler: latest, canary, feature/x veya v0.25.3 gibi sürüm.</summary>
    public static bool IsValid(string? version) =>
        !string.IsNullOrEmpty(version) && version.Length <= MaxLength && VersionPattern().IsMatch(version);

    /// <summary>"dokploy/dokploy:v0.25.3@sha256:…" biçimindeki image adından etiketi çıkarır.</summary>
    public static string? ParseImageTag(string? image)
    {
        if (string.IsNullOrWhiteSpace(image))
            return null;

        var reference = image.Split('@', 2)[0];
        var slash = reference.LastIndexOf('/');
        var colon = reference.LastIndexOf(':');
        return colon > slash ? reference[(colon + 1)..] : "latest";
    }

    [GeneratedRegex(@"^(latest|canary|feature/[A-Za-z0-9._-]+|v?\d+\.\d+\.\d+(-[A-Za-z0-9.]+)?)$")]
    private static partial Regex VersionPattern();
}
