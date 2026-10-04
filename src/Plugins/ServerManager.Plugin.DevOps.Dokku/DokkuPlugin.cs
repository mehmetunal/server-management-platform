namespace ServerManager.Plugin.DevOps.Dokku;

public static class DokkuPlugin
{
    /// <summary>plugin.json içindeki SystemName ile aynı olmalıdır.</summary>
    public const string SystemName = "DevOps.Dokku";

    public const string ServerTabKey = "dokku";

    public const string RateLimitPolicy = "dokku-action";
}
