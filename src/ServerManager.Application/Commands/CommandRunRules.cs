namespace ServerManager.Application.Commands;

public static class CommandRunRules
{
    public const int MaxCommandLength = 8000;
    public const int MaxServers = 100;
    public const int MinTimeoutSeconds = 5;
    public const int MaxTimeoutSeconds = 900;
    public const int DefaultTimeoutSeconds = 60;
    public const int MaxOutputChars = 32_000;
    public const int Parallelism = 5;

    /// <summary>Çıktının sonunu korur; hatalar genelde en sonda yazılır.</summary>
    public static (string Text, bool Truncated) TruncateOutput(string? output)
    {
        if (string.IsNullOrEmpty(output))
            return (string.Empty, false);

        return output.Length <= MaxOutputChars
            ? (output, false)
            : (output[^MaxOutputChars..], true);
    }
}
