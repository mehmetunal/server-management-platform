using ServerManager.Application.Deployments;

namespace ServerManager.Application.Tests.Deployments;

public class DeployPathsTests
{
    [Theory]
    [InlineData("/srv/apps/api")]
    [InlineData("/home/deploy/app")]
    [InlineData("/opt/acme_api-v2")]
    [InlineData("/root/apps")]
    [InlineData("/var/lib/apps/api")]
    [InlineData("/var/www/api")]
    public void Accepts_project_directories(string path) => Assert.True(DeployPaths.TryValidate(path, out _));

    [Theory]
    [InlineData("")]
    [InlineData("srv/apps")]
    [InlineData("/srv/apps/")]
    [InlineData("/srv/../etc/app")]
    [InlineData("/srv//apps")]
    [InlineData("/srv/my app")]
    [InlineData("/srv")]
    [InlineData("/home")]
    [InlineData("/etc/myapp")]
    [InlineData("/usr/local/app")]
    [InlineData("/proc/1")]
    [InlineData("/home/ubuntu")]
    [InlineData("/home/ubuntu/.ssh")]
    [InlineData("/home/ubuntu/.config/app")]
    [InlineData("/root/.ssh")]
    [InlineData("/root/.cache/app")]
    [InlineData("/var/lib/docker")]
    [InlineData("/var/lib/mysql")]
    public void Rejects_system_or_malformed_paths(string path)
    {
        Assert.False(DeployPaths.TryValidate(path, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("docker-compose.yml", true)]
    [InlineData("deploy/compose.prod.yml", true)]
    [InlineData("../docker-compose.yml", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("a//b", false)]
    [InlineData("./Dockerfile", false)]
    [InlineData("my file", false)]
    public void Relative_files_stay_inside_project(string path, bool expected) => Assert.Equal(expected, DeployPaths.IsValidRelativeFile(path));

    [Fact]
    public void Combine_joins_with_single_slash() => Assert.Equal("/srv/app/.env", DeployPaths.Combine("/srv/app/", ".env"));
}
