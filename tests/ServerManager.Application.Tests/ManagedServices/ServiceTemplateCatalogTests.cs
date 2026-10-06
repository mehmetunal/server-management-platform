using Microsoft.Extensions.Logging.Abstractions;
using ServerManager.Application.ManagedServices;
using ServerManager.Application.Plugins;
using ServerManager.Domain.Enums;
using ServerManager.Plugin.Services.Extra;

namespace ServerManager.Application.Tests.ManagedServices;

public sealed class ServiceTemplateCatalogTests : IDisposable
{
    private const string SystemName = "Test.Templates";
    private const string Prefix = "test.templates.";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "sm-template-tests", Guid.NewGuid().ToString("N"));

    public ServiceTemplateCatalogTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "Content"));
        File.WriteAllText(Path.Combine(_directory, "Content", "logo.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    internal static ServiceTemplate Valid(string key, Action<TemplateOverrides>? configure = null)
    {
        var o = new TemplateOverrides();
        configure?.Invoke(o);
        return new ServiceTemplate
        {
            Key = key,
            DisplayName = "Test servisi",
            Category = ManagedServiceCategory.Application,
            CategoryKey = o.CategoryKey,
            Description = "Test için şablon.",
            LogoFile = o.LogoFile,
            Color = "#123456",
            Image = o.Image,
            Tags = ["1.0", "latest"],
            Ports = o.Ports ?? [new ServicePortDefinition("http", 8080, ServicePortRole.WebUi, "Web")],
            DataPath = _ => o.DataPath,
            Environment = c => [new(o.EnvironmentKey, c.Password ?? string.Empty, true)],
            Credentials = new ServiceCredentialSpec { UsernameKind = ServiceUsernameKind.None },
            HealthCommand = o.HealthCommand
        };
    }

    internal sealed class TemplateOverrides
    {
        public string Image { get; set; } = "example/app";
        public string? DataPath { get; set; } = "/data";
        public string EnvironmentKey { get; set; } = "APP_PASSWORD";
        public string? HealthCommand { get; set; } = "wget -q -O /dev/null http://127.0.0.1:8080/health";
        public string LogoFile { get; set; } = "logo.svg";
        public string? CategoryKey { get; set; }
        public IReadOnlyList<ServicePortDefinition>? Ports { get; set; }
    }

    private (ServiceTemplateCatalog Catalog, PluginCatalog Plugins) Build(
        IEnumerable<IServiceTemplateProvider>? providers = null,
        IEnumerable<IServiceTemplateHooks>? hooks = null,
        bool enabled = true)
    {
        var plugin = new LoadedPlugin(
            new PluginDescriptor { SystemName = SystemName, FriendlyName = "Test Şablonları" },
            _directory,
            typeof(ServiceTemplateCatalogTests).Assembly,
            null);
        var plugins = new PluginCatalog([plugin]);
        plugins.SetState(SystemName, enabled);
        return (new ServiceTemplateCatalog(plugins, providers ?? [], hooks ?? [], NullLogger<ServiceTemplateCatalog>.Instance), plugins);
    }

    private void WriteJson(string fileName, string json)
    {
        Directory.CreateDirectory(Path.Combine(_directory, ServiceTemplateJson.FolderName));
        File.WriteAllText(Path.Combine(_directory, ServiceTemplateJson.FolderName, fileName), json);
    }

    [Fact]
    public void Built_in_templates_pass_validation()
    {
        foreach (var template in ServiceTemplates.BuiltIn)
            Assert.True(ServiceTemplateValidator.Validate(template).Count == 0, $"{template.Key}: {string.Join(" ", ServiceTemplateValidator.Validate(template))}");
    }

    [Fact]
    public void Catalog_merges_built_in_and_enabled_plugin_templates()
    {
        var (catalog, _) = Build([new Provider(Valid(Prefix + "app"))]);

        var available = catalog.GetAvailable();

        Assert.Equal(ServiceTemplates.BuiltIn.Count + 1, available.Count);
        Assert.Equal(ServiceTemplates.BuiltIn.Select(t => t.Key), available.Take(ServiceTemplates.BuiltIn.Count).Select(t => t.Key));
        var plugin = catalog.Find(Prefix + "app")!;
        Assert.Equal(SystemName, plugin.PluginSystemName);
        Assert.False(plugin.IsBuiltIn);
        Assert.Equal("app", plugin.LocalKey);
        Assert.Equal("plugins/test.templates/logo.svg", plugin.LogoPath);
        Assert.Equal("images/services/postgres.svg", catalog.Find(ServiceTemplates.Postgres)!.LogoPath);
        Assert.Empty(catalog.Issues);
    }

    [Fact]
    public void Disabled_plugin_templates_are_hidden_but_resolvable_for_existing_services()
    {
        var (catalog, plugins) = Build([new Provider(Valid(Prefix + "app"))], enabled: false);

        Assert.Null(catalog.Find(Prefix + "app"));
        Assert.DoesNotContain(catalog.GetAvailable(), t => t.Key == Prefix + "app");
        var resolution = catalog.Resolve(Prefix + "app");
        Assert.Equal(ServiceTemplateAvailability.PluginDisabled, resolution.Availability);
        Assert.NotNull(resolution.Template);
        Assert.Equal("Şablon eklentisi devre dışı", resolution.Badge);
        Assert.Contains("Test Şablonları", resolution.BlockedMessage, StringComparison.Ordinal);

        plugins.SetState(SystemName, true);

        Assert.NotNull(catalog.Find(Prefix + "app"));
        Assert.True(catalog.Resolve(Prefix + "app").IsAvailable);
    }

    [Fact]
    public void Removed_plugin_template_is_missing_and_guesses_the_plugin()
    {
        var catalog = TestTemplates.Catalog;

        var resolution = catalog.Resolve("services.extra.meilisearch");

        Assert.Equal(ServiceTemplateAvailability.Missing, resolution.Availability);
        Assert.Null(resolution.Template);
        Assert.Equal("Şablon eklentisi bulunamadı", resolution.Badge);
        Assert.True(catalog.Resolve(ServiceTemplates.Postgres).IsAvailable);
    }

    [Theory]
    [InlineData("app")]
    [InlineData("postgres")]
    [InlineData("other.plugin.app")]
    [InlineData("test.templates.App")]
    [InlineData("test.templates.")]
    public void Plugin_template_keys_must_be_namespaced(string key)
    {
        var (catalog, _) = Build([new Provider(Valid(key))]);

        // "postgres" yerleşik şablon olarak kalır; eklentinin aynı anahtarlı şablonu alınmaz.
        Assert.True(catalog.Find(key) is null or { IsBuiltIn: true });
        var issue = Assert.Single(catalog.GetIssues(SystemName));
        Assert.Contains("Key", issue, StringComparison.Ordinal);
    }

    [Fact]
    public void Colliding_keys_keep_the_first_template_and_report_the_second()
    {
        WriteJson("app.json", Json(Prefix + "app"));
        var first = Valid(Prefix + "app");
        var (catalog, _) = Build([new Provider(first)]);

        Assert.Same(first, catalog.Find(Prefix + "app"));
        var issue = Assert.Single(catalog.GetIssues(SystemName));
        Assert.Contains("çakışıyor", issue, StringComparison.Ordinal);
        Assert.StartsWith("templates/app.json", issue, StringComparison.Ordinal);
    }

    public static TheoryData<string, string> InvalidTemplates => new()
    {
        { "image", "Image" },
        { "port", "Port" },
        { "datapath", "DataPath" },
        { "envkey", "Environment" },
        { "health", "HealthCommand" },
        { "category", "CategoryKey" }
    };

    [Theory]
    [MemberData(nameof(InvalidTemplates))]
    public void Invalid_plugin_templates_are_skipped_with_reason(string kind, string expected)
    {
        var template = Valid(Prefix + "bad", o =>
        {
            switch (kind)
            {
                case "image": o.Image = "Bad Image;rm -rf /"; break;
                case "port": o.Ports = [new ServicePortDefinition("http", 70000, ServicePortRole.WebUi, "Web")]; break;
                case "datapath": o.DataPath = "data/../etc"; break;
                case "envkey": o.EnvironmentKey = "BAD-KEY"; break;
                case "health": o.HealthCommand = null; break;
                case "category": o.CategoryKey = "unknown"; break;
            }
        });
        var (catalog, _) = Build([new Provider(template)]);

        Assert.Null(catalog.Find(Prefix + "bad"));
        Assert.Contains(expected, Assert.Single(catalog.GetIssues(SystemName)), StringComparison.Ordinal);
    }

    [Fact]
    public void Shell_commands_must_be_single_line()
    {
        var errors = ServiceTemplateValidator.Validate(Valid(Prefix + "x", o => o.HealthCommand = "true\nrm -rf /"), SystemName);

        Assert.Contains(errors, e => e.Contains("HealthCommand", StringComparison.Ordinal));
    }

    [Fact]
    public void Throwing_provider_is_reported_and_does_not_break_the_catalog()
    {
        var (catalog, _) = Build([new ThrowingProvider()]);

        Assert.Equal(ServiceTemplates.BuiltIn.Count, catalog.GetAvailable().Count);
        Assert.Contains("okunamadı", Assert.Single(catalog.GetIssues(SystemName)), StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_logo_is_a_warning_not_an_error()
    {
        var (catalog, _) = Build([new Provider(Valid(Prefix + "app", o => o.LogoFile = "missing.svg"))]);

        Assert.NotNull(catalog.Find(Prefix + "app"));
        Assert.Contains("logosu bulunamadı", Assert.Single(catalog.GetIssues(SystemName)), StringComparison.Ordinal);
    }

    [Fact]
    public void Json_templates_are_loaded_from_the_plugin_folder_with_categories()
    {
        WriteJson("search.json", $$"""
            {
              "categories": [ { "key": "{{Prefix}}search", "displayName": "Arama", "order": 5 } ],
              "templates": [ {{Json(Prefix + "search", group: Prefix + "search")}} ]
            }
            """);
        WriteJson("broken.json", "{ not json");
        var (catalog, _) = Build();

        var template = catalog.Find(Prefix + "search")!;
        Assert.Equal(Prefix + "search", template.GroupKey);
        Assert.Equal(Prefix + "search", catalog.GetCategories()[0].Key);
        Assert.Contains(catalog.GetIssues(SystemName), i => i.StartsWith("templates/broken.json", StringComparison.Ordinal));
    }

    [Fact]
    public void Plugin_categories_must_be_namespaced_and_unused_categories_are_hidden()
    {
        var (catalog, _) = Build([new Provider([Valid(Prefix + "app")], [new ServiceTemplateCategory("database", "Çakışan"), new ServiceTemplateCategory(Prefix + "empty", "Boş")])]);

        Assert.Contains(catalog.GetIssues(SystemName), i => i.Contains("\"database\"", StringComparison.Ordinal));
        Assert.DoesNotContain(catalog.GetCategories(), c => c.Key == Prefix + "empty");
        Assert.Equal(["database", "application"], catalog.GetCategories().Select(c => c.Key));
    }

    [Fact]
    public void Hooks_bind_only_to_templates_of_the_same_plugin_and_only_while_enabled()
    {
        var own = new Hooks(Prefix + "app");
        var foreign = new Hooks(ServiceTemplates.Postgres);
        var (catalog, plugins) = Build([new Provider(Valid(Prefix + "app"))], [own, foreign]);

        Assert.Same(own, Assert.Single(catalog.GetHooks(Prefix + "app")));
        Assert.Empty(catalog.GetHooks(ServiceTemplates.Postgres));
        Assert.Contains(catalog.GetIssues(SystemName), i => i.Contains("kendi eklentisinin", StringComparison.Ordinal));

        plugins.SetState(SystemName, false);
        Assert.Empty(catalog.GetHooks(Prefix + "app"));
    }

    [Fact]
    public void Extra_arguments_with_secrets_or_newlines_are_rejected()
    {
        Assert.Null(ServiceTemplateValidator.ValidateExtraArguments(["start-dev", "--http-port=8080"], ["S3cret-Value"]));
        Assert.NotNull(ServiceTemplateValidator.ValidateExtraArguments(["--password=S3cret-Value"], ["S3cret-Value"]));
        Assert.NotNull(ServiceTemplateValidator.ValidateExtraArguments(["a\nb"], []));
        Assert.NotNull(ServiceTemplateValidator.ValidateExtraArguments([""], []));
    }

    [Fact]
    public void Sample_plugin_templates_are_valid_and_grouped()
    {
        var directory = SamplePluginDirectory();
        var plugin = new LoadedPlugin(
            new PluginDescriptor { SystemName = ExtraServicesPlugin.SystemName, FriendlyName = "Ek Servis Şablonları" },
            directory,
            typeof(KeycloakTemplateProvider).Assembly,
            null);
        var plugins = new PluginCatalog([plugin]);
        plugins.SetState(ExtraServicesPlugin.SystemName, true);
        var catalog = new ServiceTemplateCatalog(plugins, [new KeycloakTemplateProvider()], [new KeycloakTemplateHooks()], NullLogger<ServiceTemplateCatalog>.Instance);

        Assert.Empty(catalog.Issues);
        var meili = catalog.Find(ExtraServicesPlugin.MeilisearchKey)!;
        var clickhouse = catalog.Find(ExtraServicesPlugin.ClickHouseKey)!;
        var keycloak = catalog.Find(ExtraServicesPlugin.KeycloakKey)!;
        Assert.True(meili.Credentials.GeneratesEncryptionKey);
        Assert.Equal([8123, 9000], clickhouse.Ports.Select(p => p.ContainerPort));
        Assert.Equal(8123, clickhouse.PrimaryPort!.ContainerPort);
        Assert.Equal(ServiceUsernameKind.Name, keycloak.Credentials.UsernameKind);
        Assert.Single(catalog.GetHooks(ExtraServicesPlugin.KeycloakKey));
        Assert.Contains(catalog.GetCategories(), c => c.Key == ExtraServicesPlugin.SearchCategory);
        Assert.Contains(catalog.GetCategories(), c => c.Key == ExtraServicesPlugin.IdentityCategory);

        var credentials = new ServiceCredentials { Username = "app", Password = "P@ss word", Database = "db1", EncryptionKey = "k".PadRight(64, 'k') };
        Assert.Equal("MEILI_MASTER_KEY", Assert.Single(meili.Environment(credentials)).Key);
        Assert.Equal("http://app:P%40ss%20word@sm-svc-ch:8123/?database=db1", clickhouse.ConnectionString(new ServiceEndpoint("sm-svc-ch", 8123), credentials));
        foreach (var template in new[] { meili, clickhouse, keycloak })
            Assert.True(File.Exists(Path.Combine(directory, "Content", template.LogoFile)), template.LogoFile);
    }

    [Fact]
    public void Keycloak_hooks_choose_mode_and_validate_hostname()
    {
        var hooks = new KeycloakTemplateHooks();

        Assert.Equal(["start-dev"], hooks.BuildExtraArgs(new ServiceTemplateCommandContext { TemplateKey = hooks.TemplateKey, ImageTag = "26.3" }));
        Assert.Equal(["start"], hooks.BuildExtraArgs(new ServiceTemplateCommandContext
        {
            TemplateKey = hooks.TemplateKey,
            ImageTag = "26.3",
            Environment = new Dictionary<string, string> { ["KC_HOSTNAME"] = "https://sso.example.com" }
        }));
        Assert.Empty(hooks.Validate(new ServiceTemplateFormContext
        {
            TemplateKey = hooks.TemplateKey,
            Environment = new Dictionary<string, string> { ["KC_HOSTNAME"] = "sso.example.com" }
        }));
        Assert.Single(hooks.Validate(new ServiceTemplateFormContext
        {
            TemplateKey = hooks.TemplateKey,
            Environment = new Dictionary<string, string> { ["KC_HOSTNAME"] = "ftp://bad host" }
        }));
    }

    internal static string SamplePluginDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ServerManager.slnx")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "Plugins", "ServerManager.Plugin.Services.Extra");
    }

    internal static string Json(string key, string? group = null) => $$"""
        {
          "key": "{{key}}",
          "displayName": "JSON servisi",
          "category": "application",
          {{(group is null ? string.Empty : $"\"group\": \"{group}\",")}}
          "description": "JSON ile tanımlı.",
          "logo": "logo.svg",
          "color": "#abcdef",
          "image": "example/json-app",
          "tags": ["2"],
          "ports": [ { "name": "http", "containerPort": 3000, "role": "webUi", "label": "Web" } ],
          "healthCommand": "true"
        }
        """;

    private sealed class Provider(IReadOnlyList<ServiceTemplate> templates, IReadOnlyList<ServiceTemplateCategory>? categories = null) : IServiceTemplateProvider
    {
        public Provider(ServiceTemplate template)
            : this([template])
        {
        }

        public IReadOnlyList<ServiceTemplate> GetTemplates() => templates;

        public IReadOnlyList<ServiceTemplateCategory> GetCategories() => categories ?? [];
    }

    private sealed class ThrowingProvider : IServiceTemplateProvider
    {
        public IReadOnlyList<ServiceTemplate> GetTemplates() => throw new InvalidOperationException("bozuk");
    }

    private sealed class Hooks(string key) : IServiceTemplateHooks
    {
        public string TemplateKey => key;
    }
}
