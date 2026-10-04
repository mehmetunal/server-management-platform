namespace ServerManager.Plugin.Storage.S3;

public static class S3Plugin
{
    /// <summary>plugin.json içindeki SystemName ile aynı olmalıdır; depolama kaydında sağlayıcı adı olarak saklanır.</summary>
    public const string SystemName = "Storage.S3";

    public const string DisplayName = "S3 uyumlu depolama";

    public const string EndpointKey = "Endpoint";

    public const string RegionKey = "Region";

    public const string BucketKey = "Bucket";

    public const string PrefixKey = "Prefix";

    public const string AccessKeyKey = "AccessKey";

    public const string SecretKeyKey = "SecretKey";

    public const string PathStyleKey = "PathStyle";

    public const string DefaultRegion = "us-east-1";
}
