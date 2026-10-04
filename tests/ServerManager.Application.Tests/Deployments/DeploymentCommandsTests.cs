using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Deployments;

namespace ServerManager.Application.Tests.Deployments;

public class DeploymentCommandsTests
{
    private const string Token = "ghp_secretTOKEN123";
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>Tek tırnaklı değerin <c>sh -c '…'</c> betiği içindeki hali.</summary>
    private static string Inner(string value) => $"'\"'\"'{value}'\"'\"'";

    private static DeploymentPlan Plan(string? token = null, string? commit = null, string? environment = null, IReadOnlyList<string>? ports = null) => new()
    {
        Slug = "api",
        Source = new GitSource { RepositoryUrl = "https://github.com/acme/api.git", Username = token is null ? null : "x-access-token", AccessToken = token },
        Branch = "main",
        Commit = commit,
        DeployPath = "/srv/apps/api",
        BuildType = DeploymentBuildType.Dockerfile,
        ComposeFile = "deploy/compose.yml",
        DockerfilePath = "Dockerfile",
        PortMappings = ports ?? [],
        Environment = environment
    };

    [Fact]
    public void Token_is_sent_on_stdin_and_never_in_the_command_line()
    {
        var plan = Plan(Token);

        var fetch = DeploymentCommands.FetchSource(plan);
        var branches = DeploymentCommands.ListBranches(plan.Source);

        Assert.DoesNotContain(Token, fetch);
        Assert.DoesNotContain(Token, branches);
        Assert.Equal(Token + "\n", DeploymentCommands.TokenInput(plan.Source));
        Assert.Contains("read -r SM_GIT_TOKEN", fetch);
        Assert.Contains("$SM_GIT_TOKEN", fetch);
        Assert.Contains("credential.helper=", fetch);
    }

    [Fact]
    public void Without_token_no_stdin_is_used_and_system_helpers_are_reset()
    {
        var plan = Plan();

        Assert.Null(DeploymentCommands.TokenInput(plan.Source));
        var fetch = DeploymentCommands.FetchSource(plan);
        Assert.DoesNotContain("SM_GIT_TOKEN", fetch);
        Assert.Contains("git -c credential.helper= -C", fetch);
        Assert.Contains("GIT_TERMINAL_PROMPT=0", fetch);
        Assert.Contains("BatchMode=yes", fetch);
    }

    [Fact]
    public void Fetch_uses_branch_ref_or_exact_commit()
    {
        Assert.Contains($"fetch --depth 1 --no-tags --progress origin {Inner("refs/heads/main")}", DeploymentCommands.FetchSource(Plan()));
        Assert.Contains($"origin {Inner(Sha)}", DeploymentCommands.FetchSource(Plan(commit: Sha)));
        Assert.Contains("checkout -q --force --detach FETCH_HEAD", DeploymentCommands.FetchSource(Plan()));
        Assert.DoesNotContain("log -1", DeploymentCommands.FetchSource(Plan()));
    }

    [Fact]
    public void Read_commit_prints_machine_readable_fields_for_the_work_tree()
    {
        var script = DeploymentCommands.ReadCommit("/srv/apps/api");

        Assert.Contains($"git -C {Inner("/srv/apps/api")} log -1", script);
        Assert.Contains("%H%x1f%an%x1f%s", script);
    }

    [Fact]
    public void Prepare_never_deletes_and_refuses_non_empty_directories()
    {
        var script = DeploymentCommands.PrepareWorkspace("/srv/apps/api");

        Assert.Contains("mkdir -p", script);
        Assert.Contains($"exit {DeploymentCommands.WorkspaceNotEmptyExitCode}", script);
        Assert.DoesNotContain("rm ", script);
        Assert.DoesNotContain("clean", script);
    }

    [Fact]
    public void Environment_file_is_written_with_restricted_permissions_from_stdin()
    {
        var command = DeploymentCommands.WriteEnvironmentFile("/srv/apps/api");

        Assert.Contains("umask 077", command);
        Assert.Contains($"cat > {Inner("/srv/apps/api/.env")}", command);
    }

    [Fact]
    public void Compose_commands_use_project_name_and_file()
    {
        var plan = Plan();

        Assert.Equal(
            "docker compose --progress plain --project-name sm-api --project-directory '/srv/apps/api' -f '/srv/apps/api/deploy/compose.yml' build",
            DeploymentCommands.ComposeBuild(plan));
        Assert.EndsWith(" up -d --remove-orphans", DeploymentCommands.ComposeUp(plan));
    }

    [Fact]
    public void Docker_build_and_run_tag_image_with_commit()
    {
        var plan = Plan(environment: "A=1\n", ports: ["8080:80", "127.0.0.1:9000:9000"]);

        Assert.Equal(
            "docker build --progress=plain -t sm-api:0123456789ab -t sm-api:latest -f '/srv/apps/api/Dockerfile' '/srv/apps/api'",
            DeploymentCommands.DockerBuild(plan, Sha));
        Assert.Equal(
            "docker run -d --name sm-api --restart unless-stopped --label sm.project=api -p '8080:80' -p '127.0.0.1:9000:9000' --env-file '/srv/apps/api/.env' sm-api:0123456789ab",
            DeploymentCommands.DockerRun(plan, Sha));
        Assert.Equal("docker rm -f sm-api", DeploymentCommands.DockerRemoveContainer("api"));
    }

    [Fact]
    public void Docker_run_skips_env_file_when_no_environment_is_configured() =>
        Assert.DoesNotContain("--env-file", DeploymentCommands.DockerRun(Plan(), Sha));

    [Fact]
    public void Routes_attach_the_proxy_network_and_a_second_compose_file()
    {
        var plan = Plan();
        plan = new DeploymentPlan
        {
            Slug = plan.Slug,
            Source = plan.Source,
            Branch = plan.Branch,
            DeployPath = plan.DeployPath,
            BuildType = DeploymentBuildType.DockerCompose,
            ComposeFile = plan.ComposeFile,
            Routes =
            [
                new DeploymentRoute
                {
                    RouterName = "api-abcd1234",
                    Host = "api.ornek.com",
                    ContainerPort = 8080,
                    ServiceName = "web",
                    TlsMode = DeploymentTlsMode.Cloudflare
                }
            ]
        };

        Assert.Contains("sm-proxy.override.yml", DeploymentCommands.ComposeUp(plan), StringComparison.Ordinal);
        Assert.Contains("--no-build", DeploymentCommands.ComposeUp(plan, noBuild: true), StringComparison.Ordinal);
        var run = DeploymentCommands.DockerRun(plan, Sha);
        Assert.Contains("--network sm-proxy", run, StringComparison.Ordinal);
        Assert.Contains("traefik.enable=true", run, StringComparison.Ordinal);
    }

    [Fact]
    public void Proxy_install_quotes_the_email_and_checks_that_traefik_is_running()
    {
        var command = DeploymentCommands.InstallProxy("ops@example.com");

        Assert.Contains("traefik:v3.5", command, StringComparison.Ordinal);
        Assert.Contains("'ops@example.com'", command, StringComparison.Ordinal);
        Assert.Contains("{{.State.Running}}", command, StringComparison.Ordinal);
        Assert.Contains("SM_PROXY=busy", command, StringComparison.Ordinal);
    }

    [Fact]
    public void User_command_runs_in_project_directory_with_errexit()
    {
        var command = DeploymentCommands.RunUserCommand("/srv/apps/api", "npm ci\r\nnpm run build");

        Assert.StartsWith("sh -c '", command);
        Assert.Contains("set -e", command);
        Assert.Contains($"cd -- {Inner("/srv/apps/api")}", command);
        Assert.Contains("npm ci\nnpm run build", command);
    }

    [Fact]
    public void Remove_project_deletes_the_folder_and_leaves_the_proxy_container()
    {
        var command = DeploymentCommands.RemoveProject(Plan());

        Assert.Contains("down --remove-orphans --rmi local -v", command, StringComparison.Ordinal);
        Assert.Contains($"P={Inner("/srv/apps/api")}", command, StringComparison.Ordinal);
        Assert.Contains("rm -rf -- \"$P\"", command, StringComparison.Ordinal);
        Assert.Contains(Inner("sm-api"), command, StringComparison.Ordinal);
        Assert.DoesNotContain("docker rm -f 'sm-traefik'", command, StringComparison.Ordinal);
        Assert.DoesNotContain("docker rm -f sm-traefik", command, StringComparison.Ordinal);
    }
}
