using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.ManagedServices;

namespace ServerManager.Application.Tests.ManagedServices;

public class ManagedServiceCommandsTests
{
    private const string Password = "Sup3rS3cretPassw0rd";

    private static ManagedServicePlan Plan(
        string templateKey = ServiceTemplates.Postgres,
        string tag = "17",
        bool expose = false,
        ManagedServiceVolumeMode volumeMode = ManagedServiceVolumeMode.NamedVolume,
        string? hostPath = null,
        IReadOnlyList<string>? networks = null)
    {
        var template = TestTemplates.Find(templateKey)!;
        var credentials = new ServiceCredentials { Username = "app", Password = Password, Database = "app" };
        var bindings = template.Ports.Select(p => new ServicePortBinding(p.ContainerPort, p.ContainerPort + 10000)).ToList();
        return new ManagedServicePlan
        {
            Slug = "db",
            ContainerName = ManagedServiceNames.ContainerName("db"),
            TemplateKey = template.Key,
            Image = template.Image,
            Tag = tag,
            EnvironmentFile = ManagedServiceSettingsRules.BuildEnvironmentFile(template, credentials, "EXTRA=value with spaces\n"),
            Ports = ServicePortBindings.Published(template, bindings, expose),
            VolumeMode = volumeMode,
            HostDataPath = hostPath,
            DataPath = template.DataPath(tag),
            DataOwner = template.DataOwner,
            MemoryLimitMb = 512,
            CpuLimit = 1.5m,
            Networks = networks ?? [ManagedServiceNames.ServicesNetwork, ManagedServiceNames.ProxyNetwork, "app_default"],
            ManagedNetworks = [ManagedServiceNames.ServicesNetwork, ManagedServiceNames.ProxyNetwork],
            Command = template.Command,
            HealthCommand = template.HealthCommand,
            ReadinessCommand = template.ReadinessCommand,
            Secrets = credentials.Secrets().ToList()
        };
    }

    [Fact]
    public void Create_uses_env_file_and_never_puts_secrets_on_command_line()
    {
        foreach (var template in ServiceTemplates.BuiltIn)
        {
            var plan = Plan(template.Key, template.DefaultTag);
            var command = ManagedServiceCommands.Create(plan);

            Assert.StartsWith("docker create ", command);
            Assert.DoesNotContain(Password, command);
            Assert.Contains("--env-file '/var/lib/sm-services/db/.env'", command);
            Assert.Contains("--name 'sm-svc-db'", command);
            Assert.Contains("--restart 'unless-stopped'", command);
            Assert.Contains("--label 'sm.service=db'", command);
            Assert.Contains($"--label 'sm.template={template.Key}'", command);
            Assert.Contains($"'{template.Image}:{template.DefaultTag}'", command);
        }
    }

    [Fact]
    public void Create_binds_ports_volume_limits_and_first_network()
    {
        var command = ManagedServiceCommands.Create(Plan());

        Assert.Contains("--network 'sm-services'", command);
        Assert.DoesNotContain("app_default", command);
        Assert.Contains("-p '127.0.0.1:15432:5432/tcp'", command);
        Assert.Contains("-v 'sm-svc-db-data:/var/lib/postgresql/data'", command);
        Assert.Contains("--memory '512m'", command);
        Assert.Contains("--cpus '1.5'", command);
        Assert.Contains("--health-cmd 'pg_isready -q -h 127.0.0.1 -U \"$POSTGRES_USER\" -d \"$POSTGRES_DB\"'", command);
    }

    [Fact]
    public void Exposed_ports_bind_to_all_interfaces()
    {
        var command = ManagedServiceCommands.Create(Plan(expose: true));
        Assert.Contains("-p '0.0.0.0:15432:5432/tcp'", command);
    }

    [Fact]
    public void Host_path_mode_mounts_directory()
    {
        var command = ManagedServiceCommands.Create(Plan(volumeMode: ManagedServiceVolumeMode.HostPath, hostPath: "/srv/data/pg"));
        Assert.Contains("-v '/srv/data/pg:/var/lib/postgresql/data'", command);
    }

    [Fact]
    public void Cpu_limit_uses_invariant_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            Assert.Contains("--cpus '1.5'", ManagedServiceCommands.Create(Plan()));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Redis_command_arguments_are_quoted_and_reference_environment()
    {
        var command = ManagedServiceCommands.Create(Plan(ServiceTemplates.Redis, "8"));

        Assert.EndsWith("'redis:8' 'sh' '-c' 'exec docker-entrypoint.sh redis-server --requirepass \"$REDIS_PASSWORD\" --appendonly yes'", command);
        Assert.DoesNotContain(Password, command);
    }

    [Fact]
    public void Environment_file_is_written_from_stdin_with_restrictive_permissions()
    {
        var plan = Plan();
        var command = ManagedServiceCommands.WriteEnvironmentFile(plan.Slug);

        Assert.DoesNotContain(Password, command);
        Assert.Contains("umask 077", command);
        Assert.Contains("cat > '\"'\"'/var/lib/sm-services/db/.env'\"'\"'", command);
        Assert.Contains("chmod 600", command);
        Assert.Contains("POSTGRES_PASSWORD=" + Password + "\n", plan.EnvironmentFile);
        Assert.Contains("EXTRA=value with spaces\n", plan.EnvironmentFile);
    }

    [Fact]
    public void Values_with_quotes_are_escaped()
    {
        var command = ManagedServiceCommands.Exec("sm-svc-db", "echo 'hi'");
        Assert.Equal("docker exec 'sm-svc-db' sh -c 'echo '\"'\"'hi'\"'\"''", command);
    }

    [Fact]
    public void Console_uses_template_command_or_interactive_shell()
    {
        var template = TestTemplates.Find(ServiceTemplates.Postgres)!;
        var console = ManagedServiceCommands.Console("sm-svc-db", template.ConsoleCommand);
        var shell = ManagedServiceCommands.Console("sm-svc-db", null);

        Assert.StartsWith("docker exec -it -e TERM=xterm-256color 'sm-svc-db' sh -c '", console);
        Assert.Contains("exec psql", console);
        Assert.Contains("exec bash", shell);
    }

    [Fact]
    public void Port_check_lists_ports_and_ignores_own_container()
    {
        var command = ManagedServiceCommands.CheckPorts("sm-svc-db", [15432, 15432, 19000]);

        Assert.Contains("for p in 15432 19000; do", command);
        Assert.Contains("docker port \"$name\"", command);
        Assert.Contains("ss -ltnH", command);
        Assert.Contains(ManagedServiceCommands.PortBusyMarker, command);
    }

    [Fact]
    public void Networks_are_created_only_for_panel_networks()
    {
        var command = ManagedServiceCommands.EnsureNetworks(["sm-services"], ["app_default"]);

        Assert.Contains("docker network create --label sm.managed=true '\"'\"'sm-services'\"'\"'", command);
        Assert.DoesNotContain("docker network create --label sm.managed=true '\"'\"'app_default'\"'\"'", command);
        Assert.Contains(ManagedServiceCommands.NetworkMissingMarker, command);
        Assert.Equal("docker network connect 'app_default' 'sm-svc-db'", ManagedServiceCommands.ConnectNetwork("app_default", "sm-svc-db"));
    }

    [Fact]
    public void Host_directory_records_path_only_when_created_by_panel()
    {
        var command = ManagedServiceCommands.EnsureHostDirectory("db", "/srv/data/pg", "10001:0");

        Assert.Contains("if [ ! -e \"$P\" ] || [ -z \"$(ls -A -- \"$P\" 2>/dev/null)\" ]; then", command);
        Assert.Contains("data-path", command);
        Assert.Contains("chown '\"'\"'10001:0'\"'\"' \"$P\"", command);
        Assert.Contains("sembolik", command);
    }

    [Fact]
    public void Removal_keeps_data_by_default()
    {
        var plan = new ManagedServiceRemovalPlan { Slug = "db", ContainerName = "sm-svc-db", RemoveData = false };
        var command = ManagedServiceCommands.RemoveData(plan);

        Assert.DoesNotContain("docker volume rm", command);
        Assert.Contains("rm -rf -- \"$S\"", command);
    }

    [Fact]
    public void Removal_with_data_deletes_named_volume()
    {
        var plan = new ManagedServiceRemovalPlan { Slug = "db", ContainerName = "sm-svc-db", RemoveData = true };
        var command = ManagedServiceCommands.RemoveData(plan);

        Assert.Contains("docker volume rm \"$v\"", command);
        Assert.Contains("sm-svc-db-data", command);
    }

    [Fact]
    public void Removal_of_host_directory_requires_panel_record()
    {
        var plan = new ManagedServiceRemovalPlan
        {
            Slug = "db",
            ContainerName = "sm-svc-db",
            RemoveData = true,
            VolumeMode = ManagedServiceVolumeMode.HostPath,
            HostDataPath = "/srv/data/pg"
        };
        var command = ManagedServiceCommands.RemoveData(plan);

        Assert.Contains("data-path", command);
        Assert.Contains($"exit {ManagedServiceCommands.NotManagedExitCode}", command);
        Assert.Contains("rm -rf -- \"$P\"", command);
        Assert.Contains("case \"$P\" in /*/*)", command);
    }

    [Fact]
    public void Inspect_output_is_parsed()
    {
        var state = SshManagedServiceProvider.ParseInspect("running|healthy|2|2026-10-05T10:00:00.123456789Z|postgres:17|sm-services,sm-proxy,\n");

        Assert.Equal("running", state.State);
        Assert.Equal("healthy", state.Health);
        Assert.Equal(2, state.RestartCount);
        Assert.Equal("postgres:17", state.Image);
        Assert.Equal(["sm-services", "sm-proxy"], state.Networks);
        Assert.NotNull(state.StartedAt);

        var noHealth = SshManagedServiceProvider.ParseInspect("exited||0|0001-01-01T00:00:00Z|redis:8|\n");
        Assert.Null(noHealth.Health);
        Assert.Null(noHealth.StartedAt);
    }

    [Fact]
    public void Probe_output_is_parsed()
    {
        var probe = SshManagedServiceProvider.ParseProbe("SM_ARCH=aarch64\nSM_DOCKER=installed\nSM_DOCKER_VERSION=27.3.1\nSM_DOKPLOY=1\n");

        Assert.True(probe.DockerInstalled);
        Assert.True(probe.DockerRunning);
        Assert.Equal("27.3.1", probe.DockerVersion);
        Assert.Equal("aarch64", probe.Architecture);
        Assert.False(probe.IsX86);
        Assert.True(probe.DokployDetected);
        Assert.False(probe.DokkuDetected);

        var missing = SshManagedServiceProvider.ParseProbe("SM_ARCH=x86_64\nSM_DOCKER=missing\nSM_DOKKU=1\n");
        Assert.False(missing.DockerInstalled);
        Assert.True(missing.IsX86);
        Assert.True(missing.DokkuDetected);
    }
}
