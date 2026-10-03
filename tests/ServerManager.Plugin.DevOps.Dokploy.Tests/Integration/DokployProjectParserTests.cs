using ServerManager.Plugin.DevOps.Dokploy.Integration;

namespace ServerManager.Plugin.DevOps.Dokploy.Tests.Integration;

public class DokployProjectParserTests
{
    [Fact]
    public void Counts_resources_directly_under_project()
    {
        const string json = """
            [
              {
                "projectId": "p1",
                "name": "Shop",
                "description": "E-ticaret",
                "createdAt": "2026-09-01T10:00:00.000Z",
                "applications": [{ "applicationId": "a1", "env": "SECRET=1" }, { "applicationId": "a2" }],
                "compose": [{ "composeId": "c1" }],
                "postgres": [{ "postgresId": "pg", "databasePassword": "hunter2" }],
                "redis": [{ "redisId": "r" }],
                "mysql": []
              }
            ]
            """;

        var project = Assert.Single(DokployProjectParser.Parse(json)!);

        Assert.Equal("p1", project.Id);
        Assert.Equal("Shop", project.Name);
        Assert.Equal("E-ticaret", project.Description);
        Assert.Equal(new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), project.CreatedAt);
        Assert.Equal(0, project.EnvironmentCount);
        Assert.Equal(2, project.ApplicationCount);
        Assert.Equal(1, project.ComposeCount);
        Assert.Equal(2, project.DatabaseCount);
    }

    [Fact]
    public void Sums_resources_from_nested_environments()
    {
        const string json = """
            [
              {
                "projectId": "p2",
                "name": "Api",
                "description": "",
                "environments": [
                  { "name": "production", "applications": [{}, {}], "mariadb": [{}] },
                  { "name": "staging", "applications": [{}], "compose": [{}, {}], "mongo": [{}] }
                ]
              }
            ]
            """;

        var project = Assert.Single(DokployProjectParser.Parse(json)!);

        Assert.Null(project.Description);
        Assert.Equal(2, project.EnvironmentCount);
        Assert.Equal(3, project.ApplicationCount);
        Assert.Equal(2, project.ComposeCount);
        Assert.Equal(2, project.DatabaseCount);
    }

    [Fact]
    public void Projects_are_sorted_by_name()
    {
        var projects = DokployProjectParser.Parse("""[{"projectId":"2","name":"beta"},{"projectId":"1","name":"Alpha"}]""")!;

        Assert.Equal(["Alpha", "beta"], projects.Select(p => p.Name));
    }

    [Theory]
    [InlineData("""{"message":"Unauthorized"}""")]
    [InlineData("<html></html>")]
    public void Non_array_response_returns_null(string body)
    {
        Assert.Null(DokployProjectParser.Parse(body));
    }

    [Theory]
    [InlineData("\"v0.25.3\"", "v0.25.3")]
    [InlineData("""{"version":"v0.26.0"}""", "v0.26.0")]
    [InlineData("v0.24.1", "v0.24.1")]
    [InlineData("", null)]
    [InlineData("<html>login</html>", null)]
    public void Parses_version_response(string body, string? expected)
    {
        Assert.Equal(expected, DokployProjectParser.ParseVersion(body));
    }
}
