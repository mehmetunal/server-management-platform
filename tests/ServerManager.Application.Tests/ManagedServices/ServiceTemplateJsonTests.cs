using System.Text.Json;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.ManagedServices;

public class ServiceTemplateJsonTests
{
    private const string Full = """
        {
          "$schema": "../schema.json",
          // yorum satırları kabul edilir
          "key": "acme.db",
          "displayName": "Acme DB",
          "category": "database",
          "description": "Örnek",
          "logo": "acme.svg",
          "color": "#112233",
          "image": "registry.example.com:5000/acme/db",
          "tags": ["3.1", "3.0"],
          "ports": [
            { "name": "db", "containerPort": 5000, "role": "primary", "label": "DB" },
            { "name": "ui", "containerPort": 5001, "role": "webUi", "label": "UI", "publishByDefault": false }
          ],
          "credentials": { "username": "name", "defaultUsername": "app", "password": "standard", "database": true, "defaultDatabase": "app", "reservedUsernames": ["root"] },
          "dataPath": "/var/lib/acme",
          "dataOwner": "1000:1000",
          "environment": [
            { "key": "ACME_USER", "value": "{{username}}" },
            { "key": "ACME_PASSWORD", "value": "{{password}}", "secret": true }
          ],
          "defaultEnvironment": [ { "key": "TZ", "value": "Europe/Istanbul" } ],
          "command": ["serve", "--data", "/var/lib/acme"],
          "healthCommand": "acme ping",
          "connectionString": "acme://{{username:url}}:{{password:url}}@{{host}}:{{port}}/{{database}}",
          "suggestedEnvironment": { "ACME_URL": "acme://{{host}}:{{port}}", "ACME_ADO": "Password={{password:ado}}" },
          "minMemoryMb": 128,
          "warnOnMajorUpgrade": true,
          "healthTimeoutSeconds": 240
        }
        """;

    private static readonly ServiceCredentials Credentials = new() { Username = "app", Password = "p@ss;word", Database = "main" };

    [Fact]
    public void Full_template_is_parsed_and_placeholders_are_expanded()
    {
        var result = ServiceTemplateJson.Parse(Full, "acme.json");

        Assert.Empty(result.Errors);
        var template = Assert.Single(result.Templates);
        Assert.Equal("acme.db", template.Key);
        Assert.Equal(ManagedServiceCategory.Database, template.Category);
        Assert.Equal(ServicePortRole.WebUi, template.Ports[1].Role);
        Assert.False(template.Ports[1].PublishByDefault);
        Assert.Equal(ServicePasswordPolicy.Standard, template.Credentials.PasswordPolicy);
        Assert.Equal("/var/lib/acme", template.DataPath("3.1"));
        Assert.Equal(["serve", "--data", "/var/lib/acme"], template.Command);
        Assert.Equal(240, template.HealthTimeoutSeconds);

        var environment = template.Environment(Credentials);
        Assert.Equal(("ACME_USER", "app", false), (environment[0].Key, environment[0].Value, environment[0].Secret));
        Assert.Equal(("ACME_PASSWORD", "p@ss;word", true), (environment[1].Key, environment[1].Value, environment[1].Secret));

        var endpoint = new ServiceEndpoint("sm-svc-acme", 5000);
        Assert.Equal("acme://app:p%40ss%3Bword@sm-svc-acme:5000/main", template.ConnectionString(endpoint, Credentials));
        var suggested = template.SuggestedEnvironment(endpoint, Credentials);
        Assert.Equal("acme://sm-svc-acme:5000", suggested["ACME_URL"]);
        Assert.Equal("Password={p@ss;word}", suggested["ACME_ADO"]);

        Assert.Empty(ServiceTemplateValidator.Validate(template, "Acme"));
    }

    [Fact]
    public void Ipv6_host_is_bracketed()
    {
        var template = Assert.Single(ServiceTemplateJson.Parse(Full, "acme.json").Templates);

        Assert.StartsWith("acme://[2001:db8::1]:5000", template.SuggestedEnvironment(new ServiceEndpoint("2001:db8::1", 5000), Credentials)["ACME_URL"], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"connectionString\": \"{{secret}}\"", "bilinmeyen yer tutucu")]
    [InlineData("\"connectionString\": \"{{password:base64}}\"", "biçimi hatalı")]
    [InlineData("\"environment\": [ { \"key\": \"HOST\", \"value\": \"{{host}}\" } ]", "burada kullanılamaz")]
    [InlineData("\"defaultEnvironment\": [ { \"key\": \"P\", \"value\": \"{{password}}\" } ]", "yer tutucu kullanılamaz")]
    public void Invalid_placeholders_are_reported(string fragment, string expected)
    {
        var json = $$"""{ "key": "acme.x", "displayName": "X", "category": "application", "description": "d", "image": "acme/x", "tags": ["1"], "ports": [], {{fragment}} }""";

        var result = ServiceTemplateJson.Parse(json, "x.json");

        Assert.Empty(result.Templates);
        Assert.Contains(expected, Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ \"key\": \"acme.x\", \"unknownField\": 1 }")]
    [InlineData("{ \"key\": \"acme.x\", \"category\": \"cache\" }")]
    [InlineData("[ 1, 2 ]")]
    [InlineData("{ broken")]
    public void Malformed_files_are_reported(string json)
    {
        var result = ServiceTemplateJson.Parse(json, "x.json");

        Assert.Empty(result.Templates);
        Assert.StartsWith("x.json", Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_required_field_skips_only_that_template()
    {
        var json = """
            {
              "templates": [
                { "key": "acme.ok", "displayName": "Ok", "category": "application", "description": "d", "image": "acme/ok", "tags": ["1"], "ports": [] },
                { "key": "acme.bad", "category": "application", "description": "d", "image": "acme/bad", "tags": ["1"], "ports": [] }
              ]
            }
            """;

        var result = ServiceTemplateJson.Parse(json, "multi.json");

        Assert.Equal("acme.ok", Assert.Single(result.Templates).Key);
        Assert.Contains("displayName", Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void Sample_plugin_json_files_parse_without_errors()
    {
        var folder = Path.Combine(ServiceTemplateCatalogTests.SamplePluginDirectory(), ServiceTemplateJson.FolderName);
        var files = Directory.GetFiles(folder, "*.json");

        Assert.NotEmpty(files);
        foreach (var file in files)
            Assert.Empty(ServiceTemplateJson.Parse(File.ReadAllText(file), Path.GetFileName(file)).Errors);
    }

    [Fact]
    public void Json_schema_lists_every_template_field()
    {
        var root = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(ServiceTemplateCatalogTests.SamplePluginDirectory())))!;
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", "schemas", "service-template.schema.json")));
        var properties = schema.RootElement.GetProperty("$defs").GetProperty("template").GetProperty("properties")
            .EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        var expected = typeof(ServiceTemplateJsonModel).GetProperties()
            .Select(p => p.Name == "Schema" ? "$schema" : JsonNamingPolicy.CamelCase.ConvertName(p.Name));
        Assert.All(expected, name => Assert.Contains(name, properties));
    }
}
