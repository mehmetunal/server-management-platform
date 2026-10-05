using ServerManager.Application.Docker;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.ManagedServices;

public class ServiceTemplatesTests
{
    private const string SamplePassword = "S3cretPassw0rdXYZ";

    public static TheoryData<string> TemplateKeys()
    {
        var data = new TheoryData<string>();
        foreach (var template in ServiceTemplates.All)
            data.Add(template.Key);
        return data;
    }

    private static ServiceCredentials Credentials => new()
    {
        Username = "app",
        Password = SamplePassword,
        Database = "appdb",
        EncryptionKey = "0123456789abcdef0123456789abcdef"
    };

    [Fact]
    public void Catalog_contains_requested_databases_and_applications()
    {
        var databases = ServiceTemplates.ByCategory(ManagedServiceCategory.Database).Select(t => t.Key).ToList();
        var applications = ServiceTemplates.ByCategory(ManagedServiceCategory.Application).Select(t => t.Key).ToList();

        Assert.Equal(["postgres", "mysql", "mariadb", "redis", "mongodb", "mssql"], databases);
        Assert.Equal(["minio", "rabbitmq", "adminer", "pgadmin", "uptime-kuma", "n8n"], applications);
        Assert.Equal(ServiceTemplates.All.Count, ServiceTemplates.All.Select(t => t.Key).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(TemplateKeys))]
    public void Every_template_has_image_tags_ports_and_metadata(string key)
    {
        var template = ServiceTemplates.Find(key)!;

        Assert.False(string.IsNullOrWhiteSpace(template.DisplayName));
        Assert.False(string.IsNullOrWhiteSpace(template.Description));
        Assert.Matches("^[A-Za-z0-9]{1,3}$", template.LogoText);
        Assert.Matches("^#[0-9A-Fa-f]{6}$", template.Color);
        Assert.NotEmpty(template.Tags);
        Assert.All(template.Tags, tag =>
        {
            Assert.True(ServiceValidation.IsValidTag(tag), tag);
            Assert.True(DockerNames.IsValidImageReference(template.ImageReference(tag)), template.ImageReference(tag));
        });
        Assert.Equal(template.Tags.Count, template.Tags.Distinct().Count());
        Assert.NotEmpty(template.Ports);
        Assert.All(template.Ports, port => Assert.InRange(port.ContainerPort, 1, 65535));
        Assert.Equal(template.Ports.Count, template.Ports.Select(p => p.ContainerPort).Distinct().Count());
        Assert.NotNull(template.PrimaryPort);
        Assert.True(template.MinMemoryMb > 0);
        Assert.True(template.HealthTimeoutSeconds >= 60);
    }

    [Theory]
    [MemberData(nameof(TemplateKeys))]
    public void Data_path_is_absolute_when_defined(string key)
    {
        var template = ServiceTemplates.Find(key)!;
        foreach (var tag in template.Tags)
        {
            var path = template.DataPath(tag);
            if (path is not null)
                Assert.Matches("^/[A-Za-z0-9._/-]+$", path);
        }
    }

    [Theory]
    [MemberData(nameof(TemplateKeys))]
    public void Secret_environment_values_are_flagged_and_commands_never_contain_secrets(string key)
    {
        var template = ServiceTemplates.Find(key)!;
        var environment = template.Environment(Credentials);

        foreach (var item in environment.Where(e => e.Value == SamplePassword || e.Value == Credentials.EncryptionKey))
            Assert.True(item.Secret, $"{item.Key} gizli olarak işaretlenmeli.");

        var commands = new[] { template.HealthCommand, template.ReadinessCommand, template.ConsoleCommand }
            .Concat(template.Command)
            .Where(c => c is not null)
            .ToList();
        Assert.All(commands, command =>
        {
            Assert.DoesNotContain(SamplePassword, command);
            Assert.DoesNotContain(Credentials.EncryptionKey!, command);
        });
    }

    [Theory]
    [MemberData(nameof(TemplateKeys))]
    public void Databases_have_console_readiness_health_and_connection_strings(string key)
    {
        var template = ServiceTemplates.Find(key)!;
        if (template.Category != ManagedServiceCategory.Database)
            return;

        Assert.NotNull(template.ConsoleCommand);
        Assert.NotNull(template.ConsoleLabel);
        Assert.NotNull(template.ReadinessCommand);
        Assert.NotNull(template.HealthCommand);
        Assert.True(template.Credentials.HasPassword);

        var endpoint = new ServiceEndpoint("sm-svc-db", template.PrimaryPort!.ContainerPort);
        Assert.False(string.IsNullOrEmpty(template.ConnectionString(endpoint, Credentials)));
        Assert.NotEmpty(template.SuggestedEnvironment(endpoint, Credentials));
    }

    [Theory]
    [MemberData(nameof(TemplateKeys))]
    public void Applications_with_web_ui_expose_http_address(string key)
    {
        var template = ServiceTemplates.Find(key)!;
        if (template.WebUiPort is not { } web)
            return;

        var url = template.ConnectionString(new ServiceEndpoint("203.0.113.5", 18080), Credentials);
        if (template.Key is ServiceTemplates.RabbitMq)
            Assert.StartsWith("amqp://", url);
        else
            Assert.StartsWith("http://203.0.113.5:", url);
        Assert.True(web.ContainerPort > 0);
    }

    [Fact]
    public void Credential_environment_maps_to_documented_variables()
    {
        string[] Keys(string key) => ServiceTemplates.Find(key)!.Environment(Credentials).Select(e => e.Key).ToArray();

        Assert.Equal(["POSTGRES_USER", "POSTGRES_PASSWORD", "POSTGRES_DB"], Keys(ServiceTemplates.Postgres));
        Assert.Equal(["MYSQL_ROOT_PASSWORD", "MYSQL_USER", "MYSQL_PASSWORD", "MYSQL_DATABASE"], Keys(ServiceTemplates.MySql));
        Assert.Equal(["MARIADB_ROOT_PASSWORD", "MARIADB_USER", "MARIADB_PASSWORD", "MARIADB_DATABASE"], Keys(ServiceTemplates.MariaDb));
        Assert.Equal(["MONGO_INITDB_ROOT_USERNAME", "MONGO_INITDB_ROOT_PASSWORD", "MONGO_INITDB_DATABASE"], Keys(ServiceTemplates.MongoDb));
        Assert.Equal(["ACCEPT_EULA", "MSSQL_SA_PASSWORD"], Keys(ServiceTemplates.SqlServer));
        Assert.Equal(["MINIO_ROOT_USER", "MINIO_ROOT_PASSWORD"], Keys(ServiceTemplates.MinIo));
        Assert.Equal(["RABBITMQ_DEFAULT_USER", "RABBITMQ_DEFAULT_PASS"], Keys(ServiceTemplates.RabbitMq));
        Assert.Equal(["PGADMIN_DEFAULT_EMAIL", "PGADMIN_DEFAULT_PASSWORD"], Keys(ServiceTemplates.PgAdmin));
        Assert.Equal(["N8N_ENCRYPTION_KEY"], Keys(ServiceTemplates.N8n));
        Assert.Contains(ServiceTemplates.Find(ServiceTemplates.SqlServer)!.DefaultEnvironment, e => e.Key == "MSSQL_PID");
    }

    [Fact]
    public void Redis_password_is_passed_through_container_environment_not_argv()
    {
        var redis = ServiceTemplates.Find(ServiceTemplates.Redis)!;

        Assert.Contains("--requirepass \"$REDIS_PASSWORD\"", string.Join(' ', redis.Command));
        Assert.Equal("REDIS_PASSWORD", Assert.Single(redis.Environment(Credentials)).Key);
    }

    [Fact]
    public void Sql_server_requires_x86_and_uses_mssql_tools18()
    {
        var mssql = ServiceTemplates.Find(ServiceTemplates.SqlServer)!;

        Assert.True(mssql.RequiresX86);
        Assert.Equal(ServicePasswordPolicy.MssqlComplex, mssql.Credentials.PasswordPolicy);
        Assert.Equal(ServiceUsernameKind.Fixed, mssql.Credentials.UsernameKind);
        Assert.Contains("/opt/mssql-tools18/bin/sqlcmd -C", mssql.ConsoleCommand);
        Assert.True(mssql.MinMemoryMb >= 2048);
        Assert.All(ServiceTemplates.All.Where(t => t.Key != ServiceTemplates.SqlServer), t => Assert.False(t.RequiresX86));
    }

    [Theory]
    [InlineData("18", "/var/lib/postgresql")]
    [InlineData("latest", "/var/lib/postgresql")]
    [InlineData("17", "/var/lib/postgresql/data")]
    [InlineData("16.4-alpine", "/var/lib/postgresql/data")]
    public void Postgres_data_path_follows_image_layout(string tag, string expected)
    {
        Assert.Equal(expected, ServiceTemplates.Find(ServiceTemplates.Postgres)!.DataPath(tag));
    }

    [Theory]
    [InlineData("16", 16)]
    [InlineData("8.4", 8)]
    [InlineData("2022-latest", 2022)]
    [InlineData("4.1-management", 4)]
    [InlineData("latest", null)]
    [InlineData("lts", null)]
    [InlineData("", null)]
    public void Major_version_is_leading_number(string tag, int? expected)
    {
        Assert.Equal(expected, ServiceTemplates.MajorVersion(tag));
    }

    [Fact]
    public void Reserved_usernames_block_root_for_mysql_family()
    {
        Assert.Contains("root", ServiceTemplates.Find(ServiceTemplates.MySql)!.Credentials.ReservedUsernames);
        Assert.Contains("root", ServiceTemplates.Find(ServiceTemplates.MariaDb)!.Credentials.ReservedUsernames);
    }

    [Fact]
    public void Mysql_client_writes_password_to_temporary_option_file_inside_container()
    {
        var mysql = ServiceTemplates.Find(ServiceTemplates.MySql)!;

        Assert.Contains("--defaults-extra-file=\"$f\"", mysql.ConsoleCommand);
        Assert.Contains("\"$MYSQL_ROOT_PASSWORD\"", mysql.ConsoleCommand);
        Assert.Contains("chmod 600", mysql.ConsoleCommand);
        Assert.Contains("rm -f \"$f\"", mysql.ConsoleCommand);
    }
}
