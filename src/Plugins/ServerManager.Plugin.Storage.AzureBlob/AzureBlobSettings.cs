namespace ServerManager.Plugin.Storage.AzureBlob;

internal sealed record AzureBlobSettings(Uri ServiceUri, string Container, string Prefix, string AccountName, string AccountKey)
{
    public string BlobName(string objectKey) => Prefix + objectKey;

    public override string ToString() => $"{ServiceUri.Host}/{Container}/{Prefix}";
}
