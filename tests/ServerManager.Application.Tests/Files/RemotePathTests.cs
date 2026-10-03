using ServerManager.Application.Files;

namespace ServerManager.Application.Tests.Files;

public class RemotePathTests
{
    [Theory]
    [InlineData("/", "/")]
    [InlineData("/var/www/", "/var/www")]
    [InlineData("//var///www", "/var/www")]
    [InlineData("/var/./www/../log", "/var/log")]
    [InlineData("/../../etc", "/etc")]
    [InlineData("/srv/app name/ü.txt", "/srv/app name/ü.txt")]
    public void Normalizes_absolute_paths(string input, string expected)
    {
        Assert.Equal(expected, RemotePath.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("var/www")]
    [InlineData("~/app")]
    [InlineData("/var/\nwww")]
    [InlineData("/var/\0www")]
    public void Rejects_relative_empty_or_control_character_paths(string? input)
    {
        Assert.Null(RemotePath.Normalize(input));
    }

    [Fact]
    public void Rejects_paths_longer_than_limit()
    {
        Assert.Null(RemotePath.Normalize("/" + new string('a', RemotePath.MaxPathLength)));
    }

    [Theory]
    [InlineData("app.json", true)]
    [InlineData(".env", true)]
    [InlineData("name with spaces", true)]
    [InlineData("", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("a/b", false)]
    [InlineData("tab\tname", false)]
    public void Validates_entry_names(string name, bool expected)
    {
        Assert.Equal(expected, RemotePath.IsValidName(name));
    }

    [Fact]
    public void Rejects_names_longer_than_limit()
    {
        Assert.False(RemotePath.IsValidName(new string('a', RemotePath.MaxNameLength + 1)));
    }

    [Theory]
    [InlineData("/", null)]
    [InlineData("/var", "/")]
    [InlineData("/var/www/app", "/var/www")]
    public void Gets_parent(string path, string? expected)
    {
        Assert.Equal(expected, RemotePath.GetParent(path));
    }

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/var/www/app.json", "app.json")]
    public void Gets_file_name(string path, string expected)
    {
        Assert.Equal(expected, RemotePath.GetFileName(path));
    }

    [Fact]
    public void Combines_directory_and_name()
    {
        Assert.Equal("/app.json", RemotePath.Combine("/", "app.json"));
        Assert.Equal("/var/www/app.json", RemotePath.Combine("/var/www", "app.json"));
    }

    [Fact]
    public void Builds_breadcrumbs_from_root()
    {
        var crumbs = RemotePath.GetBreadcrumbs("/var/www");

        Assert.Equal(new[] { ("/", "/"), ("var", "/var"), ("www", "/var/www") }, crumbs);
    }

    [Theory]
    [InlineData("/var/www", "/var/www", true)]
    [InlineData("/var/www/app", "/var/www", true)]
    [InlineData("/anything", "/", true)]
    [InlineData("/var/www2", "/var/www", false)]
    [InlineData("/var", "/var/www", false)]
    public void Checks_same_or_descendant(string path, string ancestor, bool expected)
    {
        Assert.Equal(expected, RemotePath.IsSameOrDescendant(path, ancestor));
    }
}
