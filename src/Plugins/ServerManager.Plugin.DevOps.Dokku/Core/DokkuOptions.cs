namespace ServerManager.Plugin.DevOps.Dokku.Core;

public sealed class DokkuOptions
{
    public const string SectionName = "Dokku";

    public string Version { get; set; } = "v0.38.31";

    public int CommandTimeoutSeconds { get; set; } = 60;

    public int InstallTimeoutMinutes { get; set; } = 20;
}
