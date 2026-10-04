using System.Text.RegularExpressions;

namespace ServerManager.Application.Cloud;

/// <summary>Sunucu oluştururken verilecek user-data'yı şablon ve isteğe bağlı SSH genel anahtarından üretir.</summary>
public static partial class CloudInitBuilder
{
    public const int MaxUserDataBytes = 32 * 1024;

    private const string CloudConfigHeader = "#cloud-config";
    private const string AuthorizedKeysKey = "ssh_authorized_keys:";

    public static bool IsValidPublicKey(string? key) => key is not null && PublicKeyPattern().IsMatch(key.Trim());

    public static string? Build(string? template, string? sshPublicKey)
    {
        var content = string.IsNullOrWhiteSpace(template) ? null : template.Replace("\r\n", "\n").TrimEnd() + "\n";
        var key = string.IsNullOrWhiteSpace(sshPublicKey) ? null : sshPublicKey.Trim();

        if (key is null)
            return content;

        if (content is null)
            return $"{CloudConfigHeader}\n{AuthorizedKeysKey}\n  - {key}\n";

        if (content.StartsWith("#!", StringComparison.Ordinal))
            return InsertIntoScript(content, key);

        return InsertIntoCloudConfig(content, key);
    }

    private static string InsertIntoCloudConfig(string content, string key)
    {
        var lines = content.Split('\n').ToList();
        var index = lines.FindIndex(line => line.StartsWith(AuthorizedKeysKey, StringComparison.Ordinal));
        if (index < 0)
            return content + $"{AuthorizedKeysKey}\n  - {key}\n";

        lines.Insert(index + 1, $"  - {key}");
        return string.Join('\n', lines);
    }

    private static string InsertIntoScript(string content, string key)
    {
        var firstLineEnd = content.IndexOf('\n');
        var commands =
            "install -d -m 700 /root/.ssh\n" +
            $"printf '%s\\n' '{key}' >> /root/.ssh/authorized_keys\n" +
            "chmod 600 /root/.ssh/authorized_keys\n";
        return content[..(firstLineEnd + 1)] + commands + content[(firstLineEnd + 1)..];
    }

    [GeneratedRegex(@"^(ssh-(rsa|ed25519|dss)|ecdsa-sha2-nistp(256|384|521)|sk-(ssh-ed25519|ecdsa-sha2-nistp256)@openssh\.com) [A-Za-z0-9+/]+={0,3}( [^\r\n'""\\]{0,200})?$")]
    private static partial Regex PublicKeyPattern();
}
