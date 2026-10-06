using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Enums;
using ServerManager.E2E.Tests.Infrastructure;

namespace ServerManager.E2E.Tests;

/// <summary>
/// Dockerfile projesinin gerçek deployment'ı. Depo, sunucunun kendisinde çıplak (bare) bir git deposudur ve
/// <c>file://</c> adresiyle verilir (doğrulayıcı https, http, ssh, scp biçimi ve file:// adreslerini kabul eder);
/// böylece dışarıya (GitHub vb.) bağımlılık olmadan clone → build → run zinciri sınanır.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class DeploymentTests(E2EFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Dockerfile_project_from_bare_git_repo_deploys_and_serves_http()
    {
        await fixture.RequireAsync();

        var name = E2EFixture.Unique("e2e-app");
        var marker = "e2e-deploy-ok-" + Guid.NewGuid().ToString("N")[..8];
        var home = (await fixture.Shell.RunCheckedAsync("printf %s \"$HOME\"", cancellationToken: Ct)).Trim();
        var repo = $"{home}/e2e-git/{name}.git";
        var work = $"{home}/e2e-work/{name}";
        var deployPath = $"{home}/e2e-apps/{name}";
        var hostPort = Random.Shared.Next(20000, 29999);

        await fixture.Shell.WriteFileAsync($"{work}/Dockerfile",
            "FROM busybox:1.36\n" +
            $"RUN mkdir -p /www && echo {marker} > /www/index.html\n" +
            "EXPOSE 8080\n" +
            "CMD [\"httpd\", \"-f\", \"-v\", \"-p\", \"8080\", \"-h\", \"/www\"]\n", Ct);
        await fixture.Shell.RunCheckedAsync(
            $"set -e; git init -q --bare -b main {repo}; cd {work}; git init -q -b main; " +
            "git -c user.name=E2E -c user.email=e2e@example.invalid add Dockerfile; " +
            "git -c user.name=E2E -c user.email=e2e@example.invalid commit -q -m 'E2E: ilk sürüm'; " +
            $"git remote add origin {repo}; git push -q origin main",
            cancellationToken: Ct);

        Guid? projectId = null;
        try
        {
            var repositoryUrl = $"file://{repo}";
            var branches = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IProjectService>().ListRemoteBranchesAsync(new RemoteBranchQueryDto
            {
                ServerId = fixture.ServerId,
                RepositoryUrl = repositoryUrl
            }, Ct));
            Assert.True(branches.IsSuccess, E2EFixture.Describe(branches));
            Assert.Contains("main", branches.Data!);

            var created = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IProjectService>().CreateAsync(new CreateProjectDto
            {
                ServerId = fixture.ServerId,
                Name = name,
                GitProvider = GitProvider.SelfHosted,
                RepositoryUrl = repositoryUrl,
                Branch = "main",
                DeployPath = deployPath,
                BuildType = DeploymentBuildType.Dockerfile,
                DockerfilePath = "Dockerfile",
                PortMappings = $"{hostPort}:8080"
            }, Ct));
            Assert.True(created.IsSuccess, E2EFixture.Describe(created));
            projectId = created.Data;

            var deploymentId = await fixture.AsAdminAsync(async sp =>
            {
                var begin = await sp.GetRequiredService<IDeploymentService>().BeginAsync(projectId.Value, new StartDeploymentDto(), fixture.DeploymentActor, Ct);
                Assert.True(begin.IsSuccess, E2EFixture.Describe(begin));
                return begin.Data;
            });

            var observer = new RecordingObserver();
            await fixture.AsAdminAsync(async sp =>
            {
                using var cancellation = new DeploymentCancellation(Ct);
                var run = await sp.GetRequiredService<IDeploymentService>().RunAsync(deploymentId, fixture.DeploymentActor, observer, cancellation);
                Assert.True(run.IsSuccess, $"Deployment başarısız: {run.Message}\n{observer.Tail()}");
            });

            var details = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IDeploymentService>().GetAsync(deploymentId, includeLog: true, Ct));
            Assert.True(details.IsSuccess, details.Message);
            Assert.Equal(DeploymentStatus.Succeeded, details.Data!.Status);
            Assert.False(string.IsNullOrEmpty(details.Data.CommitSha));

            var body = await Waiter.UntilAsync(
                () => fixture.Shell.RunAsync($"curl -fsS --max-time 5 http://127.0.0.1:{hostPort}/", cancellationToken: Ct),
                r => r.ExitCode == 0 && r.Output.Contains(marker, StringComparison.Ordinal),
                TimeSpan.FromMinutes(1),
                "Deploy edilen uygulama HTTP yanıtı vermedi");
            Assert.Contains(marker, body.Output, StringComparison.Ordinal);

            var deleted = await fixture.AsAdminAsync(sp => sp.GetRequiredService<IProjectService>().DeleteAsync(projectId.Value, name, hardDelete: true, Ct));
            Assert.True(deleted.IsSuccess, E2EFixture.Describe(deleted));
            projectId = null;

            var after = await fixture.Shell.RunAsync($"curl -fsS --max-time 3 http://127.0.0.1:{hostPort}/", cancellationToken: Ct);
            Assert.NotEqual(0, after.ExitCode);
            var folder = await fixture.Shell.RunAsync($"test -e {deployPath}", cancellationToken: Ct);
            Assert.NotEqual(0, folder.ExitCode);
        }
        finally
        {
            if (projectId is { } id)
                await fixture.AsAdminAsync(sp => sp.GetRequiredService<IProjectService>().DeleteAsync(id, name, hardDelete: true, CancellationToken.None));
            await fixture.Shell.TryRunAsync(
                $"rm -rf {repo} {work} {deployPath}; rmdir --ignore-fail-on-non-empty {home}/e2e-git {home}/e2e-work {home}/e2e-apps");
        }
    }
}
