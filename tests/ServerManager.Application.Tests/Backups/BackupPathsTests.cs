using ServerManager.Application.Backups;

namespace ServerManager.Application.Tests.Backups;

public class BackupPathsTests
{
    [Theory]
    [InlineData("/etc/nginx", "/etc/nginx")]
    [InlineData(" /srv//apps/ ", "/srv/apps")]
    [InlineData("/", "/")]
    public void Normalize_cleans_absolute_paths(string input, string expected)
    {
        Assert.Equal(expected, BackupPaths.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("etc/nginx")]
    [InlineData("/srv/../etc")]
    [InlineData("/srv/./app")]
    [InlineData("/srv/a\nb")]
    public void Normalize_rejects_unsafe_paths(string? input)
    {
        Assert.Null(BackupPaths.Normalize(input));
    }

    [Theory]
    [InlineData("/proc", true)]
    [InlineData("/sys/kernel", true)]
    [InlineData("/dev", true)]
    [InlineData("/devices", false)]
    [InlineData("/srv/proc", false)]
    public void Pseudo_file_systems_are_detected(string path, bool expected)
    {
        Assert.Equal(expected, BackupPaths.IsPseudoFileSystem(path));
    }

    [Fact]
    public void Split_lines_trims_and_removes_duplicates()
    {
        Assert.Equal(["/etc", "/srv"], BackupPaths.SplitLines(" /etc \r\n\n/srv\n/etc\n"));
    }

    [Fact]
    public void Archive_name_is_relative()
    {
        Assert.Equal("etc/nginx", BackupPaths.ToArchiveName("/etc/nginx"));
    }
}
