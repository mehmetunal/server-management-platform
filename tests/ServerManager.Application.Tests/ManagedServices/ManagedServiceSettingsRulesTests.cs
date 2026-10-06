using Microsoft.Extensions.Options;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.ManagedServices;
using ServerManager.Application.Validators.ManagedServices;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Tests.ManagedServices;

public class ManagedServiceSettingsRulesTests
{
    private static readonly ServiceTemplate Postgres = TestTemplates.Find(ServiceTemplates.Postgres)!;

    private static CreateManagedServiceDto ValidDto() => new()
    {
        ServerId = Guid.NewGuid(),
        TemplateKey = ServiceTemplates.Postgres,
        Name = "Ana DB",
        ImageTag = "17",
        Username = "app",
        Password = "Str0ngPassword123",
        Database = "app",
        Ports = [new ServicePortFormItem { ContainerPort = 5432, Publish = true, HostPort = 15432 }]
    };

    private static CreateManagedServiceDtoValidator Validator(bool allowPrivileged = false) =>
        new(Options.Create(new ManagedServiceOptions { AllowPrivilegedHostPorts = allowPrivileged }), TestTemplates.Catalog);

    private static List<string> Errors(CreateManagedServiceDto dto, bool allowPrivileged = false) =>
        Validator(allowPrivileged).Validate(dto).Errors.Select(e => e.PropertyName).ToList();

    [Fact]
    public void Valid_form_passes()
    {
        Assert.Empty(Errors(ValidDto()));
    }

    [Fact]
    public void Unknown_template_and_bad_tag_are_rejected()
    {
        var dto = ValidDto();
        dto.TemplateKey = "oracle";
        dto.ImageTag = "16; rm -rf /";

        var errors = Errors(dto);
        Assert.Contains(nameof(CreateManagedServiceDto.TemplateKey), errors);
        Assert.Contains(nameof(CreateManagedServiceDto.ImageTag), errors);
    }

    [Fact]
    public void Credentials_are_validated_per_template()
    {
        var dto = ValidDto();
        dto.Username = "1bad";
        dto.Password = "short";
        dto.Database = "bad-name";
        Assert.Equal(["Username", "Password", "Database"], Errors(dto));

        var mysql = ValidDto();
        mysql.TemplateKey = ServiceTemplates.MySql;
        mysql.ImageTag = "8.4";
        mysql.Username = "root";
        mysql.Ports = [new ServicePortFormItem { ContainerPort = 3306, Publish = false }];
        Assert.Equal(["Username"], Errors(mysql));

        var pgadmin = ValidDto();
        pgadmin.TemplateKey = ServiceTemplates.PgAdmin;
        pgadmin.ImageTag = "9";
        pgadmin.Username = "admin";
        pgadmin.Ports = [new ServicePortFormItem { ContainerPort = 80, Publish = true, HostPort = 18080 }];
        Assert.Equal(["Username"], Errors(pgadmin));
    }

    [Fact]
    public void Ports_must_be_valid_unique_and_not_privileged()
    {
        var dto = ValidDto();
        dto.Ports = [new ServicePortFormItem { ContainerPort = 5432, Publish = true, HostPort = 80 }];
        Assert.Equal(["Ports[0].HostPort"], Errors(dto));
        Assert.Empty(Errors(dto, allowPrivileged: true));

        dto.Ports = [new ServicePortFormItem { ContainerPort = 5432, Publish = true, HostPort = null }];
        Assert.Equal(["Ports[0].HostPort"], Errors(dto));

        dto.Ports = [new ServicePortFormItem { ContainerPort = 9999, Publish = true, HostPort = 19999 }];
        Assert.Equal(["Ports[0].ContainerPort"], Errors(dto));

        var minio = ValidDto();
        minio.TemplateKey = ServiceTemplates.MinIo;
        minio.ImageTag = "latest";
        minio.Database = null;
        minio.Ports =
        [
            new ServicePortFormItem { ContainerPort = 9000, Publish = true, HostPort = 19000 },
            new ServicePortFormItem { ContainerPort = 9001, Publish = true, HostPort = 19000 }
        ];
        Assert.Equal(["Ports[1].HostPort"], Errors(minio));
    }

    [Fact]
    public void Unpublished_port_needs_no_host_port()
    {
        var dto = ValidDto();
        dto.Ports = [new ServicePortFormItem { ContainerPort = 5432, Publish = false, HostPort = null }];
        Assert.Empty(Errors(dto));
    }

    [Fact]
    public void Allowed_sources_limits_and_networks_are_validated()
    {
        var dto = ValidDto();
        dto.AllowedSourceIps = "203.0.113.0/24\nnot-an-ip";
        dto.MemoryLimitMb = 16;
        dto.CpuLimit = 0.05m;
        dto.Networks = "host";

        Assert.Equal(["AllowedSourceIps", "MemoryLimitMb", "CpuLimit", "Networks"], Errors(dto));
    }

    [Fact]
    public void Host_path_is_required_in_host_path_mode()
    {
        var dto = ValidDto();
        dto.VolumeMode = ManagedServiceVolumeMode.HostPath;
        dto.HostDataPath = "/etc/pg";
        Assert.Equal(["HostDataPath"], Errors(dto));

        dto.HostDataPath = "/srv/data/pg";
        Assert.Empty(Errors(dto));
    }

    [Fact]
    public void Environment_rejects_reserved_duplicate_and_multiline_entries()
    {
        var entries = new List<EnvironmentEntryDto>
        {
            new() { Key = "POSTGRES_PASSWORD", Value = "x" },
            new() { Key = "TZ", Value = "UTC" },
            new() { Key = "TZ", Value = "UTC" },
            new() { Key = "1BAD", Value = "x" },
            new() { Key = "OK", Value = "line1\nline2" },
            new() { Key = "", Value = "" }
        };

        var errors = ManagedServiceSettingsRules.ValidateEnvironment(Postgres, entries).Select(e => e.Property).ToList();
        Assert.Equal(["Environment[0].Key", "Environment[2].Key", "Environment[3].Key", "Environment[4].Value"], errors);
    }

    [Fact]
    public void Environment_file_combines_defaults_extras_and_credentials()
    {
        var mssql = TestTemplates.Find(ServiceTemplates.SqlServer)!;
        var credentials = new ServiceCredentials { Username = "sa", Password = "Passw0rd!" };

        var file = ManagedServiceSettingsRules.BuildEnvironmentFile(mssql, credentials, "MSSQL_PID=Express\nMSSQL_SA_PASSWORD=hijack\nTZ=UTC\n");

        Assert.Equal("MSSQL_PID=Express\nTZ=UTC\nACCEPT_EULA=Y\nMSSQL_SA_PASSWORD=Passw0rd!\n", file);
    }

    [Fact]
    public void Environment_text_round_trips()
    {
        var entries = new List<EnvironmentEntryDto>
        {
            new() { Key = "A", Value = "1=2" },
            new() { Key = " ", Value = "" },
            new() { Key = "B", Value = "" }
        };

        var text = ManagedServiceSettingsRules.ToEnvironmentText(entries);
        Assert.Equal("A=1=2\nB=\n", text);
        var parsed = ManagedServiceSettingsRules.FromEnvironmentText(text);
        Assert.Equal(["A", "B"], parsed.Select(e => e.Key));
        Assert.Equal("1=2", parsed[0].Value);
        Assert.Null(ManagedServiceSettingsRules.ToEnvironmentText([]));
    }

    [Fact]
    public void Update_validator_requires_template_context()
    {
        var validator = new UpdateManagedServiceDtoValidator(Options.Create(new ManagedServiceOptions()));
        var dto = new UpdateManagedServiceDto { Ports = [new ServicePortFormItem { ContainerPort = 5432, Publish = true, HostPort = 15432 }] };

        Assert.False(validator.Validate(dto).IsValid);

        var context = new FluentValidation.ValidationContext<UpdateManagedServiceDto>(dto);
        context.RootContextData[UpdateManagedServiceDtoValidator.TemplateKey] = Postgres;
        Assert.True(validator.Validate(context).IsValid);
    }

    [Fact]
    public void Published_ports_follow_template_order_and_bind_address()
    {
        var minio = TestTemplates.Find(ServiceTemplates.MinIo)!;
        var bindings = new[] { new ServicePortBinding(9001, 19001), new ServicePortBinding(9000, 19000), new ServicePortBinding(1234, 1234) };

        var local = ServicePortBindings.Published(minio, bindings, exposePublicly: false);
        var open = ServicePortBindings.Published(minio, bindings, exposePublicly: true);

        Assert.Equal([9000, 9001], local.Select(p => p.ContainerPort));
        Assert.All(local, p => Assert.Equal("127.0.0.1", p.BindAddress));
        Assert.All(open, p => Assert.Equal("0.0.0.0", p.BindAddress));
        Assert.Equal(bindings.Length, ServicePortBindings.FromJson(ServicePortBindings.ToJson(bindings)).Count);
        Assert.Empty(ServicePortBindings.FromJson("not json"));
    }
}
