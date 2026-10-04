using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ServerManager.Application.Agent;

public static partial class AgentTokens
{
    public const string Prefix = "sma_";

    public static string Generate() =>
        Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool IsWellFormed(string? token) => token is not null && TokenPattern().IsMatch(token);

    [GeneratedRegex("^sma_[A-Za-z0-9_-]{43}$")]
    private static partial Regex TokenPattern();
}
