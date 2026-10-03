using ServerManager.Application.Deployments;

namespace ServerManager.Application.Tests.Deployments;

public class DeploymentNamesTests
{
    [Theory]
    [InlineData("Müşteri API", "musteri-api")]
    [InlineData("  Çağrı  Şube Öğün ", "cagri-sube-ogun")]
    [InlineData("web_app.v2", "web-app-v2")]
    [InlineData("---", "app")]
    [InlineData(null, "app")]
    public void Slugify_produces_docker_safe_names(string? name, string expected)
    {
        var slug = DeploymentNames.Slugify(name);

        Assert.Equal(expected, slug);
        Assert.True(DeploymentNames.IsValidSlug(slug));
    }

    [Fact]
    public void Slug_is_limited_and_suffix_keeps_limit()
    {
        var slug = DeploymentNames.Slugify(new string('a', 100));
        var suffixed = DeploymentNames.WithSuffix(slug, 12);

        Assert.Equal(DeploymentNames.MaxSlugLength, slug.Length);
        Assert.Equal(DeploymentNames.MaxSlugLength, suffixed.Length);
        Assert.EndsWith("-12", suffixed);
        Assert.True(DeploymentNames.IsValidSlug(suffixed));
    }

    [Fact]
    public void Docker_names_are_prefixed()
    {
        Assert.Equal("sm-api", DeploymentNames.ComposeProjectName("api"));
        Assert.Equal("sm-api", DeploymentNames.ContainerName("api"));
        Assert.Equal("sm-api", DeploymentNames.ImageName("api"));
    }
}
