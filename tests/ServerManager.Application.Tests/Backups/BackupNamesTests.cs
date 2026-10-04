using ServerManager.Application.Backups;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.Backups;

public class BackupNamesTests
{
    private static readonly Guid JobId = Guid.Parse("0f8e5b7a-1c2d-4e3f-9a8b-7c6d5e4f3a2b");
    private static readonly Guid RunId = Guid.Parse("a1b2c3d4-0000-0000-0000-000000000000");
    private static readonly DateTime StartedAt = new(2026, 3, 10, 3, 0, 5, DateTimeKind.Utc);

    [Theory]
    [InlineData(BackupSourceType.Files, false, ".tar.gz")]
    [InlineData(BackupSourceType.DockerVolume, true, ".tar.gz.smbk")]
    [InlineData(BackupSourceType.Database, false, ".sql.gz")]
    [InlineData(BackupSourceType.Database, true, ".sql.gz.smbk")]
    public void Object_key_uses_job_folder_and_type_extension(BackupSourceType type, bool encrypted, string extension)
    {
        var key = BackupNames.ObjectKey(JobId, RunId, StartedAt, type, encrypted);

        Assert.Equal($"{JobId:N}/20260310-030005-a1b2c3d4{extension}", key);
        Assert.True(BackupNames.IsJobObjectKey(JobId, key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0f8e5b7a1c2d4e3f9a8b7c6d5e4f3a2b/../secret.tar.gz")]
    [InlineData("0f8e5b7a1c2d4e3f9a8b7c6d5e4f3a2b/20260310-030005-a1b2c3d4.tar.gz/x")]
    [InlineData("0f8e5b7a1c2d4e3f9a8b7c6d5e4f3a2b/notes.txt")]
    [InlineData("ffffffffffffffffffffffffffffffff/20260310-030005-a1b2c3d4.tar.gz")]
    [InlineData("/0f8e5b7a1c2d4e3f9a8b7c6d5e4f3a2b/20260310-030005-a1b2c3d4.tar.gz")]
    public void Only_backup_files_of_the_same_job_are_deletable(string? key)
    {
        Assert.False(BackupNames.IsJobObjectKey(JobId, key));
    }

    [Fact]
    public void Decrypted_file_name_drops_the_smbk_extension()
    {
        Assert.Equal("app-db-20260310-030005.sql.gz", BackupNames.DecryptedFileName("app-db-20260310-030005.sql.gz.smbk"));
        Assert.Equal("app-20260310-030005.tar.gz", BackupNames.DecryptedFileName("app-20260310-030005.tar.gz"));
    }

    [Fact]
    public void File_name_is_slugified()
    {
        Assert.Equal("uretim-db-20260310-030005.sql.gz.smbk", BackupNames.FileName("Üretim DB", StartedAt, BackupSourceType.Database, true));
    }
}
