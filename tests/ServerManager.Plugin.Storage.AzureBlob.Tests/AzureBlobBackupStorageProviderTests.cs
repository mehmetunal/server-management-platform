using ServerManager.Plugin.Storage.AzureBlob;

namespace ServerManager.Plugin.Storage.AzureBlob.Tests;

public class AzureBlobBackupStorageProviderTests
{
    private const string Key = "0f8e5b7a1c2d4e3f9a8b7c6d5e4f3a2b/20260310-030005-a1b2c3d4.tar.gz";
    private const string FakeKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private readonly AzureBlobBackupStorageProvider _provider = new();

    private static Dictionary<string, string> Settings(Action<Dictionary<string, string>>? change = null)
    {
        var settings = new Dictionary<string, string>
        {
            [AzureBlobPlugin.AccountNameKey] = "devstoreaccount1",
            [AzureBlobPlugin.AccountKeyKey] = FakeKey,
            [AzureBlobPlugin.EndpointKey] = "http://127.0.0.1:10000/devstoreaccount1",
            [AzureBlobPlugin.ContainerKey] = "sm-backups",
            [AzureBlobPlugin.PrefixKey] = "server-manager"
        };
        change?.Invoke(settings);
        return settings;
    }

    [Fact]
    public void Valid_settings_have_no_errors_and_parse()
    {
        Assert.Empty(_provider.Validate(Settings()));
        Assert.True(AzureBlobBackupStorageProvider.TryRead(Settings(), out var azure));
        Assert.Equal("server-manager/" + Key, azure.BlobName(Key));
        Assert.Equal("http://127.0.0.1:10000/devstoreaccount1", azure.ServiceUri.AbsoluteUri.TrimEnd('/'));
        Assert.DoesNotContain(FakeKey, azure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_endpoint_uses_azure_host()
    {
        Assert.True(AzureBlobBackupStorageProvider.TryRead(Settings(s => s[AzureBlobPlugin.EndpointKey] = ""), out var azure));
        Assert.Equal("https://devstoreaccount1.blob.core.windows.net/", azure.ServiceUri.AbsoluteUri);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Upper")]
    [InlineData("has-hyphen")]
    public void Invalid_account_is_rejected(string account)
    {
        Assert.Contains(_provider.Validate(Settings(s => s[AzureBlobPlugin.AccountNameKey] = account)), e => e.PropertyName == AzureBlobPlugin.AccountNameKey);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("SM-Backups")]
    [InlineData("-bucket")]
    [InlineData("sm--backups")]
    public void Invalid_container_is_rejected(string container)
    {
        Assert.Contains(_provider.Validate(Settings(s => s[AzureBlobPlugin.ContainerKey] = container)), e => e.PropertyName == AzureBlobPlugin.ContainerKey);
    }

    [Theory]
    [InlineData("ftp://host")]
    [InlineData("https://user:pass@host")]
    [InlineData("https://host/path?x=1")]
    [InlineData("https://host/../secret")]
    public void Invalid_endpoint_is_rejected(string endpoint)
    {
        Assert.Contains(_provider.Validate(Settings(s => s[AzureBlobPlugin.EndpointKey] = endpoint)), e => e.PropertyName == AzureBlobPlugin.EndpointKey);
    }

    [Theory]
    [InlineData("/root")]
    [InlineData("a/../b")]
    [InlineData("a b")]
    public void Unsafe_prefix_is_rejected(string prefix)
    {
        Assert.Null(AzureBlobBackupStorageProvider.NormalizePrefix(prefix));
        Assert.Contains(_provider.Validate(Settings(s => s[AzureBlobPlugin.PrefixKey] = prefix)), e => e.PropertyName == AzureBlobPlugin.PrefixKey);
    }
}
