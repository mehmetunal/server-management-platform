using Microsoft.Extensions.Options;
using ServerManager.Application.Backups;
using ServerManager.Infrastructure.Backups;

namespace ServerManager.Application.Tests.Backups;

public sealed class LocalBackupStorageProviderTests : IDisposable
{
    private const string Key = "0f8e5b7a1c2d4e3f9a8b7c6d5e4f3a2b/20260310-030005-a1b2c3d4.tar.gz";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "sm-backup-tests-" + Guid.NewGuid().ToString("N"));
    private readonly LocalBackupStorageProvider _provider;
    private readonly Dictionary<string, string> _settings = new() { [LocalBackupStorageProvider.FolderKey] = "daily" };

    public LocalBackupStorageProviderTests()
    {
        _provider = new LocalBackupStorageProvider(Options.Create(new BackupOptions { LocalRootPath = _root }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_read_and_delete_round_trip()
    {
        var upload = await _provider.UploadAsync(_settings, Key, new MemoryStream([1, 2, 3]), Ct);
        Assert.True(upload.IsSuccess);

        var read = await _provider.OpenReadAsync(_settings, Key, Ct);
        Assert.True(read.IsSuccess);
        await using (var stream = read.Data!)
        {
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy, Ct);
            Assert.Equal([1, 2, 3], copy.ToArray());
        }

        Assert.True((await _provider.DeleteAsync(_settings, Key, Ct)).IsSuccess);
        Assert.False((await _provider.OpenReadAsync(_settings, Key, Ct)).IsSuccess);
        Assert.True((await _provider.DeleteAsync(_settings, Key, Ct)).IsSuccess);
    }

    [Fact]
    public async Task Failed_upload_leaves_no_partial_file()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => _provider.UploadAsync(_settings, Key, new FailingStream(), Ct));

        var folder = Path.Combine(_root, "daily");
        Assert.Empty(Directory.Exists(folder) ? Directory.GetFiles(folder, "*", SearchOption.AllDirectories) : []);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("a/b")]
    [InlineData(".hidden")]
    [InlineData("")]
    public void Folder_outside_the_root_is_rejected(string folder)
    {
        var errors = _provider.Validate(new Dictionary<string, string> { [LocalBackupStorageProvider.FolderKey] = folder });

        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("0f8e5b7a1c2d4e3f9a8b7c6d5e4f3a2b/../../x.tar.gz")]
    [InlineData("/etc/passwd")]
    public async Task Object_key_cannot_escape_the_folder(string key)
    {
        Assert.False((await _provider.UploadAsync(_settings, key, new MemoryStream([1]), Ct)).IsSuccess);
        Assert.False((await _provider.OpenReadAsync(_settings, key, Ct)).IsSuccess);
        Assert.False((await _provider.DeleteAsync(_settings, key, Ct)).IsSuccess);
    }

    [Fact]
    public async Task Test_writes_and_cleans_up()
    {
        var result = await _provider.TestAsync(_settings, Ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "daily")));
    }
}
