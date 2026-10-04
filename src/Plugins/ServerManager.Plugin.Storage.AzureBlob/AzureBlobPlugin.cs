namespace ServerManager.Plugin.Storage.AzureBlob;

public static class AzureBlobPlugin
{
    /// <summary>plugin.json içindeki SystemName ile aynı olmalıdır; depolama kaydında sağlayıcı adı olarak saklanır.</summary>
    public const string SystemName = "Storage.AzureBlob";

    public const string DisplayName = "Azure Blob Storage";

    public const string AccountNameKey = "AccountName";

    public const string AccountKeyKey = "AccountKey";

    public const string EndpointKey = "Endpoint";

    public const string ContainerKey = "Container";

    public const string PrefixKey = "Prefix";
}
