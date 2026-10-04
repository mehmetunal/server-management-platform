using ServerManager.Plugin.Storage.S3;

namespace ServerManager.Plugin.Storage.S3.Tests;

public class S3BackupStorageProviderTests
{
    private const string Key = "0f8e5b7a1c2d4e3f9a8b7c6d5e4f3a2b/20260310-030005-a1b2c3d4.tar.gz";

    private readonly S3BackupStorageProvider _provider = new();

    private static Dictionary<string, string> Settings(Action<Dictionary<string, string>>? change = null)
    {
        var settings = new Dictionary<string, string>
        {
            [S3Plugin.EndpointKey] = "http://127.0.0.1:9000",
            [S3Plugin.RegionKey] = "us-east-1",
            [S3Plugin.BucketKey] = "sm-backups",
            [S3Plugin.PrefixKey] = "server-manager",
            [S3Plugin.AccessKeyKey] = "minioadmin",
            [S3Plugin.SecretKeyKey] = "not-a-real-secret",
            [S3Plugin.PathStyleKey] = "true"
        };
        change?.Invoke(settings);
        return settings;
    }

    [Fact]
    public void Valid_settings_have_no_errors_and_parse()
    {
        Assert.Empty(_provider.Validate(Settings()));
        Assert.True(S3BackupStorageProvider.TryRead(Settings(), out var s3));
        Assert.Equal("server-manager/" + Key, s3.Key(Key));
        Assert.True(s3.PathStyle);
        Assert.DoesNotContain("not-a-real-secret", s3.ToString());
    }

    [Theory]
    [InlineData("Upper")]
    [InlineData("ab")]
    [InlineData("-bucket")]
    [InlineData("bucket_name")]
    public void Invalid_bucket_is_rejected(string bucket)
    {
        Assert.Contains(_provider.Validate(Settings(s => s[S3Plugin.BucketKey] = bucket)), e => e.PropertyName == S3Plugin.BucketKey);
    }

    [Theory]
    [InlineData("ftp://host")]
    [InlineData("not a url")]
    [InlineData("https://host/path")]
    [InlineData("https://user:pass@host")]
    public void Invalid_endpoint_is_rejected(string endpoint)
    {
        Assert.Contains(_provider.Validate(Settings(s => s[S3Plugin.EndpointKey] = endpoint)), e => e.PropertyName == S3Plugin.EndpointKey);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("backups", "backups/")]
    [InlineData("a/b/", "a/b/")]
    [InlineData(" team-1/daily ", "team-1/daily/")]
    public void Prefix_is_normalized(string input, string expected)
    {
        Assert.Equal(expected, S3BackupStorageProvider.NormalizePrefix(input));
    }

    [Theory]
    [InlineData("/root")]
    [InlineData("a/../b")]
    [InlineData("a b")]
    [InlineData("/")]
    public void Unsafe_prefix_is_rejected(string prefix)
    {
        Assert.Null(S3BackupStorageProvider.NormalizePrefix(prefix));
        Assert.Contains(_provider.Validate(Settings(s => s[S3Plugin.PrefixKey] = prefix)), e => e.PropertyName == S3Plugin.PrefixKey);
    }

    [Fact]
    public void Missing_endpoint_uses_aws_region_and_empty_region_defaults()
    {
        var settings = Settings(s =>
        {
            s.Remove(S3Plugin.EndpointKey);
            s.Remove(S3Plugin.RegionKey);
            s.Remove(S3Plugin.PrefixKey);
        });

        Assert.True(S3BackupStorageProvider.TryRead(settings, out var s3));
        Assert.Null(s3.Endpoint);
        Assert.Equal(S3Plugin.DefaultRegion, s3.Region);
        Assert.Equal(Key, s3.Key(Key));
    }

    [Fact]
    public void Missing_secret_cannot_be_read()
    {
        Assert.False(S3BackupStorageProvider.TryRead(Settings(s => s.Remove(S3Plugin.SecretKeyKey)), out _));
    }

    [Theory]
    [InlineData("../escape.tar.gz")]
    [InlineData("0f8e5b7a1c2d4e3f9a8b7c6d5e4f3a2b/../x")]
    [InlineData("other/key")]
    public async Task Invalid_object_key_is_refused_without_network(string key)
    {
        var ct = TestContext.Current.CancellationToken;

        Assert.False((await _provider.UploadAsync(Settings(), key, new MemoryStream([1]), ct)).IsSuccess);
        Assert.False((await _provider.OpenReadAsync(Settings(), key, ct)).IsSuccess);
        Assert.False((await _provider.DeleteAsync(Settings(), key, ct)).IsSuccess);
    }

    [Fact]
    public void Secret_field_is_marked_secret()
    {
        Assert.Equal(ServerManager.Application.Notifications.NotificationFieldType.Secret,
            _provider.Fields.Single(f => f.Key == S3Plugin.SecretKeyKey).Type);
    }
}
